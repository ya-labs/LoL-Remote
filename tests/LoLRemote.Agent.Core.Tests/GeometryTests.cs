using LoLRemote.Agent.Core.Geometry;

namespace LoLRemote.Agent.Core.Tests;

public class GeometryTests
{
    private static readonly PixelRect WholeFrame1280 = new(0, 0, 1280, 720);

    [Fact]
    public void Frame_with_same_aspect_fills_the_video()
    {
        var layout = VideoLayout.Fit(1920, 1080, 1280, 720);

        Assert.Equal(new PixelRect(0, 0, 1280, 720), layout.Content);
    }

    [Fact]
    public void Wider_frame_gets_bars_on_top_and_bottom()
    {
        // Mesma conta do compositor do spike: escala 0,5, 1280x360, centralizado.
        var layout = VideoLayout.Fit(2560, 720, 1280, 720);

        Assert.Equal(new PixelRect(0, 180, 1280, 360), layout.Content);
    }

    [Fact]
    public void Taller_frame_gets_bars_on_the_sides()
    {
        var layout = VideoLayout.Fit(986, 593, 1280, 720);

        Assert.Equal(720, layout.Content.Height);
        Assert.True(layout.Content.X > 0);
        var rightBar = 1280 - layout.Content.Right;
        Assert.InRange(rightBar - layout.Content.X, 0, 1);
    }

    [Theory]
    [InlineData(0, 720)]
    [InlineData(720, 0)]
    public void Fit_rejects_empty_sizes(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => VideoLayout.Fit(width, height, 1280, 720));
    }

    [Fact]
    public void Center_touch_maps_to_center_of_client_area()
    {
        var layout = VideoLayout.Fit(1280, 720, 1280, 720);

        var result = CoordinateMapper.TryMap(new NormalizedPoint(0.5, 0.5), layout, WholeFrame1280, out var point);

        Assert.Equal(MappingFailure.None, result);
        Assert.Equal(new ClientPoint(640, 360), point);
    }

    [Fact]
    public void Title_bar_offset_is_subtracted()
    {
        // Frame 1280x752 = barra de título de 32 px + área cliente 1280x720.
        var layout = VideoLayout.Fit(1280, 752, 1280, 720);
        var client = new PixelRect(0, 32, 1280, 720);
        var touchY = (layout.Content.Y + ((32 + 100) * layout.Scale)) / 720.0;
        var touchX = (layout.Content.X + (200 * layout.Scale)) / 1280.0;

        var result = CoordinateMapper.TryMap(new NormalizedPoint(touchX, touchY), layout, client, out var point);

        Assert.Equal(MappingFailure.None, result);
        Assert.InRange(point.X, 199, 201);
        Assert.InRange(point.Y, 99, 101);
    }

    [Fact]
    public void Touch_on_title_bar_is_refused()
    {
        var layout = VideoLayout.Fit(1280, 752, 1280, 720);
        var client = new PixelRect(0, 32, 1280, 720);
        var touchY = (layout.Content.Y + (10 * layout.Scale)) / 720.0;

        Assert.Equal(MappingFailure.OutsideClientArea, CoordinateMapper.TryMap(new NormalizedPoint(0.5, touchY), layout, client, out _));
    }

    [Fact]
    public void Touch_on_letterbox_is_refused()
    {
        var layout = VideoLayout.Fit(2560, 720, 1280, 720);

        Assert.Equal(MappingFailure.Letterbox, CoordinateMapper.TryMap(new NormalizedPoint(0.5, 0.1), layout, new PixelRect(0, 0, 2560, 720), out _));
    }

    [Theory]
    [InlineData(-0.01, 0.5)]
    [InlineData(0.5, 1.01)]
    [InlineData(double.NaN, 0.5)]
    [InlineData(0.5, double.PositiveInfinity)]
    public void Invalid_coordinates_are_refused(double x, double y)
    {
        var layout = VideoLayout.Fit(1280, 720, 1280, 720);

        Assert.Equal(MappingFailure.InvalidCoordinates, CoordinateMapper.TryMap(new NormalizedPoint(x, y), layout, WholeFrame1280, out _));
    }

    [Fact]
    public void Bottom_right_edge_is_clamped_or_refused_never_outside()
    {
        var layout = VideoLayout.Fit(1280, 720, 1280, 720);

        var result = CoordinateMapper.TryMap(new NormalizedPoint(1, 1), layout, WholeFrame1280, out _);

        Assert.Equal(MappingFailure.Letterbox, result);
    }

    [Fact]
    public void Absolute_input_covers_virtual_desktop_corners()
    {
        var desktop = new PixelRect(-1920, 0, 3840, 1080);

        Assert.Equal((0, 0), CoordinateMapper.ToAbsoluteInput(-1920, 0, desktop));
        Assert.Equal((65535, 65535), CoordinateMapper.ToAbsoluteInput(1919, 1079, desktop));
    }

    [Fact]
    public void Absolute_input_rejects_points_outside_desktop()
    {
        var desktop = new PixelRect(0, 0, 1920, 1080);

        Assert.Throws<ArgumentOutOfRangeException>(() => CoordinateMapper.ToAbsoluteInput(1920, 10, desktop));
    }
}
