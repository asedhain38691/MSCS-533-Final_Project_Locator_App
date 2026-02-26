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

    // Movement gating
    private bool _movementStarted = false;
    private Location? _baseline;
    private const double MovementThresholdMeters = 30; // tweak: 20–50m

    // Heat styling
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

        // Avoid multiple loops if the page re-appears
        if (_cts != null) return;

        try
        {
            var permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (permission != PermissionStatus.Granted)
            {
                StatusLabel.Text = "Location permission not granted.";
                return;
            }

            await _db.InitAsync();

            // Reset movement state each time we start
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

                // Establish baseline on first fix
                if (_baseline == null)
                {
                    _baseline = current;
                    await MainThread.InvokeOnMainThreadAsync(() =>
                        StatusLabel.Text = $"Baseline set. Waiting for movement…");
                    continue;
                }

                // Gate until movement begins
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

                    // Movement started: clear DB + clear map + start fresh (Option B)
                    _movementStarted = true;

                    await _db.ClearAllAsync();

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        MapView.MapElements.Clear();
                        StatusLabel.Text = "Movement started ✅ Logging now…";
                    });
                }

                // Save point
                await _db.InsertAsync(new LocationPoint
                {
                    Latitude = loc.Latitude,
                    Longitude = loc.Longitude,
                    TimestampUtc = DateTime.UtcNow
                });

                // Keep map centered roughly on current location
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    StatusLabel.Text = $"Saved: {loc.Latitude:F6}, {loc.Longitude:F6}";
                    MapView.MoveToRegion(MapSpan.FromCenterAndRadius(current, Distance.FromKilometers(0.5)));
                });

                // Render heatmap
                await RenderHeatmapAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // expected when page disappears
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

            // Center on last point
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