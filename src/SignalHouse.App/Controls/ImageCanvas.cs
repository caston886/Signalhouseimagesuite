using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using SignalHouse.App.ViewModels;
using SignalHouse.Core.Imaging;

namespace SignalHouse.App.Controls;

/// <summary>
/// Displays the active editor's image with live color/detail adjustments
/// applied directly by Skia on every frame -- no intermediate CPU bitmap
/// render in the hot path -- and implements the shared canvas controls:
///   left-click + drag   -> pan
///   scroll wheel        -> zoom
///   right-click + drag  -> free rotate
///   Tab (while focused) -> commit the current edit
/// Bind this control's DataContext to an EditorViewModelBase (or a subclass).
/// </summary>
public sealed class ImageCanvas : Control
{
    private Point? _lastPointerPosition;
    private bool _isPanning;
    private bool _isRotating;

    public ImageCanvas()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is EditorViewModelBase viewModel)
        {
            viewModel.PreviewInvalidated -= OnPreviewInvalidated;
            viewModel.PreviewInvalidated += OnPreviewInvalidated;
        }
    }

    private void OnPreviewInvalidated(object? sender, EventArgs e) => InvalidateVisual();

    private EditorViewModelBase? ViewModel => DataContext as EditorViewModelBase;

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var viewModel = ViewModel;
        var document = viewModel?.Document;
        if (viewModel is null || document is null)
        {
            return;
        }

        context.Custom(new DrawOperation(new Rect(Bounds.Size), document, viewModel));
    }

    // --- Mouse: pan / zoom / rotate ------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        var point = e.GetCurrentPoint(this);
        _lastPointerPosition = point.Position;

        if (point.Properties.IsLeftButtonPressed)
        {
            _isPanning = true;
        }
        else if (point.Properties.IsRightButtonPressed)
        {
            _isRotating = true;
        }

        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_lastPointerPosition is not { } last || ViewModel is not { } viewModel)
        {
            return;
        }

        var current = e.GetPosition(this);
        var delta = current - last;

        if (_isPanning)
        {
            viewModel.PanX += delta.X;
            viewModel.PanY += delta.Y;
        }
        else if (_isRotating)
        {
            var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
            var previousAngle = Math.Atan2(last.Y - center.Y, last.X - center.X);
            var currentAngle = Math.Atan2(current.Y - center.Y, current.X - center.X);
            var deltaDegrees = (currentAngle - previousAngle) * (180 / Math.PI);
            viewModel.RotationDegrees += deltaDegrees;
        }

        _lastPointerPosition = current;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isPanning = false;
        _isRotating = false;
        _lastPointerPosition = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (ViewModel is not { } viewModel)
        {
            return;
        }

        const double zoomStep = 1.1;
        var factor = e.Delta.Y > 0 ? zoomStep : 1 / zoomStep;
        viewModel.Zoom = Math.Clamp(viewModel.Zoom * factor, 0.1, 16.0);
        e.Handled = true;
    }

    // --- Keyboard: Tab commits the current edit -------------------------------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key != Key.Tab || ViewModel is not { } viewModel)
        {
            return;
        }

        if (viewModel.CommitEditCommand.CanExecute(null))
        {
            viewModel.CommitEditCommand.Execute(null);
        }

        // Suppress the default Tab focus-navigation while the canvas itself
        // is the active surface -- Tab's job here is "commit", not "move
        // focus to the next control".
        e.Handled = true;
    }

    /// <summary>
    /// The actual Skia draw, run directly against the render surface via
    /// Avalonia's custom-draw-operation extension point. Pan/zoom/rotation
    /// are applied as canvas transforms; the color/clarity adjustments are
    /// applied as a single SKPaint.ImageFilter, so the whole frame -- image
    /// plus every slider's effect -- is one GPU-composited draw call.
    /// </summary>
    private sealed class DrawOperation : ICustomDrawOperation
    {
        private readonly ImageDocument _document;
        private readonly EditorViewModelBase _viewModel;

        public DrawOperation(Rect bounds, ImageDocument document, EditorViewModelBase viewModel)
        {
            Bounds = bounds;
            _document = document;
            _viewModel = viewModel;
        }

        public Rect Bounds { get; }

        public void Dispose()
        {
        }

        public bool Equals(ICustomDrawOperation? other) => false;

        public bool HitTest(Point p) => Bounds.Contains(p);

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature is null)
            {
                return;
            }

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            var bitmap = _document.PreviewSource;

            canvas.Save();
            try
            {
                canvas.ClipRect(new SKRect(0, 0, (float)Bounds.Width, (float)Bounds.Height));
                canvas.Translate((float)(Bounds.Width / 2 + _viewModel.PanX), (float)(Bounds.Height / 2 + _viewModel.PanY));
                canvas.Scale((float)_viewModel.Zoom);
                canvas.RotateDegrees((float)_viewModel.RotationDegrees);
                canvas.Translate(-bitmap.Width / 2f, -bitmap.Height / 2f);

                using var paint = new SKPaint
                {
                    ImageFilter = ColorAdjustmentEngine.BuildImageFilter(_document.CurrentEdit),
                    FilterQuality = SKFilterQuality.Medium,
                };
                canvas.DrawBitmap(bitmap, 0, 0, paint);

                // The crop guide is drawn here, inside the same transformed
                // coordinate space as the bitmap above (after the
                // translate/scale/rotate/re-center stack), so it stays glued
                // to the image under any pan/zoom/rotate the user applies --
                // no separate transform math needed.
                if (_viewModel is ICropOverlayProvider { CropOverlayNormalized: { } normalized })
                {
                    DrawCropOverlay(canvas, bitmap, normalized);
                }
            }
            finally
            {
                canvas.Restore();
            }
        }

        private static void DrawCropOverlay(SKCanvas canvas, SKBitmap bitmap, SKRect normalized)
        {
            var cropRect = new SKRect(
                normalized.Left * bitmap.Width,
                normalized.Top * bitmap.Height,
                normalized.Right * bitmap.Width,
                normalized.Bottom * bitmap.Height);

            using var dim = new SKPaint { Color = new SKColor(0, 0, 0, 140) };
            var full = new SKRect(0, 0, bitmap.Width, bitmap.Height);

            // Darken everything outside the crop rectangle in four strips,
            // so the crop itself is left at full brightness without needing
            // an off-screen mask buffer.
            canvas.DrawRect(SKRect.Create(full.Left, full.Top, full.Width, cropRect.Top - full.Top), dim);
            canvas.DrawRect(SKRect.Create(full.Left, cropRect.Bottom, full.Width, full.Bottom - cropRect.Bottom), dim);
            canvas.DrawRect(SKRect.Create(full.Left, cropRect.Top, cropRect.Left - full.Left, cropRect.Height), dim);
            canvas.DrawRect(SKRect.Create(cropRect.Right, cropRect.Top, full.Right - cropRect.Right, cropRect.Height), dim);

            using var stroke = new SKPaint
            {
                Color = SKColors.White,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = Math.Max(1f, bitmap.Width / 500f),
                IsAntialias = true,
            };
            canvas.DrawRect(cropRect, stroke);
        }
    }
}
