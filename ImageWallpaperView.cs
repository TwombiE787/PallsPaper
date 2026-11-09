using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace PallsPaper;

internal sealed class ImageWallpaperView : Control
{
    private readonly Image _image;
    private readonly bool _isAnimated;

    public ImageWallpaperView(string imagePath)
    {
        var source = Image.FromFile(imagePath);
        _isAnimated = ImageAnimator.CanAnimate(source);
        if (_isAnimated)
        {
            _image = source;
        }
        else
        {
            try
            {
                var normalizedImage = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppPArgb);
                using (var graphics = Graphics.FromImage(normalizedImage))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height));
                }

                _image = normalizedImage;
            }
            finally
            {
                source.Dispose();
            }
        }

        BackColor = Color.Black;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.UserPaint,
            true);

        if (_isAnimated)
        {
            ImageAnimator.Animate(_image, OnFrameChanged);
        }
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0 || _image.Width <= 0 || _image.Height <= 0)
        {
            return;
        }

        eventArgs.Graphics.SmoothingMode = SmoothingMode.HighSpeed;
        eventArgs.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
        if (_isAnimated)
        {
            ImageAnimator.UpdateFrames(_image);
        }

        eventArgs.Graphics.DrawImage(_image, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_isAnimated)
            {
                ImageAnimator.StopAnimate(_image, OnFrameChanged);
            }
            _image.Dispose();
        }

        base.Dispose(disposing);
    }

    private void OnFrameChanged(object? sender, EventArgs eventArgs)
    {
        if (IsDisposed || Disposing || !IsHandleCreated)
        {
            return;
        }

        try
        {
            BeginInvoke((Action)Invalidate);
        }
        catch (InvalidOperationException) when (IsDisposed || Disposing)
        {
        }
    }
}
