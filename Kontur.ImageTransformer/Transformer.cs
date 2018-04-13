using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;

namespace Kontur.ImageTransformer
{
    public class Transformer
    {
        private Image _image;

        public Transformer(Image image)
        {
            _image = image;
        }

        public async Task Transform(TransformType transformType)
        {
            await Task.Factory.StartNew(() =>
            {
                switch (transformType)
                {
                    case TransformType.RotateCw:
                        _image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                        break;
                    case TransformType.RotateCCW:
                        _image.RotateFlip(RotateFlipType.Rotate270FlipNone);
                        break;
                    case TransformType.FlipV:
                        _image.RotateFlip(RotateFlipType.RotateNoneFlipY);
                        break;
                    case TransformType.FlipH:
                        _image.RotateFlip(RotateFlipType.RotateNoneFlipX);
                        break;
                }
            });
        }

        public async Task<bool> TryCrop(Rectangle crop)
        {
            var imgRect = new Rectangle(0, 0, _image.Width,_image.Height);

            if (imgRect.IntersectsWith(crop))
            {
                await Task.Factory.StartNew(() => { 
                    var bmp = new Bitmap(crop.Width, crop.Height);
                    using (var gr = Graphics.FromImage(bmp))
                    {
                        gr.DrawImage(_image, new Rectangle(0, 0, bmp.Width, bmp.Height), crop, GraphicsUnit.Pixel);
                    }
                    _image = bmp;
                });
                return true;
            }
            return false;
        }

        public void WriteImage(Stream stream)
        {
            _image.Save(stream, ImageFormat.Png);
        }
    }
}