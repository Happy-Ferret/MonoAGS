﻿using AGS.API;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Drawing.Brushes;
using SixLabors.ImageSharp.Processing.Text;
using System;

namespace AGS.Engine
{
    public class ImageSharpTextDraw : IBitmapTextDraw
    {
        private ITextConfig _config;
        private Image<Rgba32> _bitmap;
        private int _maxWidth, _height;
        private string _text;

        private class DummyDisposable : IDisposable
        {
            public void Dispose() {}
        }

        DummyDisposable _dummy = new DummyDisposable();

        public ImageSharpTextDraw(Image<Rgba32> bitmap)
        {
            _bitmap = bitmap;
        }

        public IDisposable CreateContext()
        {
            _bitmap.Clear(Rgba32.Transparent);
            return _dummy;
        }

        public void DrawText(string text, ITextConfig config, API.SizeF textSize, API.SizeF baseSize,
            int maxWidth, int height, float xOffset)
        {
            _height = height;
            _text = text;
            _config = config;
            _maxWidth = maxWidth;
            IFont font = _config.Font;

            _bitmap.Mutate(gfx =>
            {
                IBrush outlineBrush = _config.OutlineBrush;

                float left = xOffset + _config.AlignX(textSize.Width, baseSize);
                float top = _config.AlignY(_bitmap.Height, textSize.Height, baseSize);
                float centerX = left + _config.OutlineWidth / 2f;
                float centerY = top + _config.OutlineWidth / 2f;
                float right = left + _config.OutlineWidth;
                float bottom = top + _config.OutlineWidth;

                if (_config.OutlineWidth > 0f)
                {
                    drawString(gfx, outlineBrush, left, top);
                    drawString(gfx, outlineBrush, centerX, top);
                    drawString(gfx, outlineBrush, right, top);

                    drawString(gfx, outlineBrush, left, centerY);
                    drawString(gfx, outlineBrush, right, centerY);

                    drawString(gfx, outlineBrush, left, bottom);
                    drawString(gfx, outlineBrush, centerX, bottom);
                    drawString(gfx, outlineBrush, right, bottom);
                }
                if (_config.ShadowBrush != null)
                {
                    drawString(gfx, _config.ShadowBrush, centerX + _config.ShadowOffsetX,
                        centerY + _config.ShadowOffsetY);
                }
                drawString(gfx, _config.Brush, centerX, centerY);
            });
        }

        private void drawString(IImageProcessingContext<Rgba32> context, IBrush ibrush, float x, float y)
        {
            IBrush<Rgba32> brush = getBrush(ibrush);
            if (brush == null)
                return;
            SixLabors.Fonts.Font font = getFont(_config.Font);

            if (_maxWidth == int.MaxValue)
            {
                var options = new TextGraphicsOptions(true);
                context.DrawText(options, _text, font, brush, new SixLabors.Primitives.PointF(x, y));
            }
            else
            {
                //todo: draw in rectangle (not constrained by height)
                var options = new TextGraphicsOptions(true) { WrapTextWidth = _maxWidth };
                context.DrawText(options, _text, font, brush, new SixLabors.Primitives.PointF(x, y));
                //gfx.DrawString(_text, font, brush, new System.Drawing.RectangleF(x, y, _maxWidth, _height),
                //    _wrapFormat);
            }
        }

        private IBrush<Rgba32> getBrush(IBrush brush)
        {
            return ((ImageSharpBrush)brush).InnerBrush; //todo: build brush based on BrushType
        }

        private SixLabors.Fonts.Font getFont(IFont font)
        {
            return ((ImageSharpFont)font).InnerFont;
        }
    }
}
