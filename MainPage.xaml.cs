using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Controls.Maps;
using MauiHeatMap.Data;
using MauiHeatMap.Models;

namespace MauiHeatMap;

public partial class MainPage : ContentPage
{
    private readonly LocationDb _db;

    private CancellationTokenSource? _cts;
   
    private bool _movementStarted = false;
    private Location? _baseline;
    private const double MovementThresholdMeters = 30;

    // Styling the movement renders. Each point will be shown with dark blue blob on the map
    private static readonly Color HeatBlue = Color.FromRgba(0, 0, 255, 0.8);
    private const double HeatRadiusMeters = 40;

    public MainPage(LocationDb db)
    {
        InitializeComponent();
        _db = db;
        StatusLabel.Text = "Initializing...";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Avoids multiple loops if the page re-appears
        if (_cts != null) return;

        try
        {
            var permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (permission != PermissionStatus.Granted)
            {
				//If the user declines the location permission
                StatusLabel.Text = "Location permission not granted.";
                return;
            }

            await _db.InitAsync();

            // Resets movement state each time the app starts
            _movementStarted = false;
            _baseline = null;

            StatusLabel.Text = "Waiting for movement…";

            _cts = new CancellationTokenSource();
            _ = TrackLoopAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Init error: {ex.Message}";
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _cts?.Cancel();
        _cts = null;
    }

    private async Task TrackLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(10));
                var loc = await Geolocation.GetLocationAsync(request, token);

                if (loc == null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                        StatusLabel.Text = "Location unavailable…");
                    continue;
                }

                var current = new Location(loc.Latitude, loc.Longitude);

                // Establishes baseline state
                if (_baseline == null)
                {
                    _baseline = current;
                    await MainThread.InvokeOnMainThreadAsync(() =>
                        StatusLabel.Text = $"Baseline set. Waiting for movement…");
                    continue;
                }

                // Waits until some movement is done
                if (!_movementStarted)
                {
                    var movedKm = Location.CalculateDistance(_baseline, current, DistanceUnits.Kilometers);
                    var movedMeters = movedKm * 1000.0;

                    if (movedMeters < MovementThresholdMeters)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                            StatusLabel.Text = $"Waiting for movement… moved {movedMeters:F0}m");
                        continue;
                    }

                    // Movement started: cleared DB with clean map and start fresh
                    _movementStarted = true;

                    await _db.ClearAllAsync();

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        MapView.MapElements.Clear();
                        StatusLabel.Text = "Movement started ✅ Logging now…";
                    });
                }

                // Saving to database
                await _db.InsertAsync(new LocationPoint
                {
                    Latitude = loc.Latitude,
                    Longitude = loc.Longitude,
                    TimestampUtc = DateTime.UtcNow
                });

        
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    StatusLabel.Text = $"Saved: {loc.Latitude:F6}, {loc.Longitude:F6}";
                    MapView.MoveToRegion(MapSpan.FromCenterAndRadius(current, Distance.FromKilometers(0.5)));
                });

                // Rendering heatmap
                await RenderHeatmapAsync();
            }
        }
        catch (OperationCanceledException)
        {
          
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                StatusLabel.Text = $"Track error: {ex.Message}");
        }
    }

    private async Task RenderHeatmapAsync()
    {
        var points = await _db.GetAllAsync();
        if (points.Count == 0) return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            MapView.MapElements.Clear();

            // Centering the map on the last point
            var last = points[^1];
            MapView.MoveToRegion(MapSpan.FromCenterAndRadius(
                new Location(last.Latitude, last.Longitude),
                Distance.FromKilometers(1)));

            foreach (var p in points)
            {
                MapView.MapElements.Add(new Circle
                {
                    Center = new Location(p.Latitude, p.Longitude),
                    Radius = Distance.FromMeters(HeatRadiusMeters),
                    StrokeWidth = 0,
                    FillColor = HeatBlue
                });
            }
        });
    }
}