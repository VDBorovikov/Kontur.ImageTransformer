using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Kontur.ImageTransformer
{
    internal class AsyncHttpServer : IDisposable
    {
        public AsyncHttpServer()
        {
            listener = new HttpListener();
        }

        public void Start(string prefix)
        {
            lock (listener)
            {
                if (!isRunning)
                {
                    listener.Prefixes.Clear();
                    listener.Prefixes.Add(prefix);
                    listener.Start();

                    listenerThread = new Thread(Listen)
                    {
                        IsBackground = true,
                        Priority = ThreadPriority.Highest
                    };
                    listenerThread.Start();

                    isRunning = true;
                }
            }
        }

        public void Stop()
        {
            lock (listener)
            {
                if (!isRunning)
                    return;

                listener.Stop();

                listenerThread.Abort();
                listenerThread.Join();

                isRunning = false;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            Stop();

            listener.Close();
        }

        private void Listen()
        {
            while (true)
            {
                try
                {
                    if (listener.IsListening)
                    {
                        var context = listener.GetContext();
                        Task.Run(() => HandleContextAsync(context));
                    }
                    else Thread.Sleep(0);
                }
                catch (ThreadAbortException)
                {
                    return;
                }
                catch (Exception)
                {
                    // TODO: log errors
                }
            }
        }

        private async Task HandleContextAsync(HttpListenerContext listenerContext)
        {
            TransformType transformType;
            Rectangle rect;
            Image image;

            if (!TryGetArgumentsFromPath(listenerContext.Request.Url, out transformType, out rect))
            {
                listenerContext.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                listenerContext.Response.Close();
                return;
            }
            
            if (!ValidateImage(listenerContext.Request, out image))
            {
                listenerContext.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                listenerContext.Response.Close();
                return;
            }

            var transformer = new Transformer(image);
            await transformer.Transform(transformType);
            if (!await transformer.TryCrop(rect))
            {
                listenerContext.Response.StatusCode = (int) HttpStatusCode.NoContent;
                listenerContext.Response.Close();
                return;
            }

            transformer.WriteImage(listenerContext.Response.OutputStream);
            listenerContext.Response.Close();
        }

        private bool TryGetArgumentsFromPath(Uri uri, out TransformType transformType, out Rectangle rect)
        {
            var match = _matcher.Match(uri.LocalPath);
            var transformGroup = match.Groups["transformType"];
            var rectGroup = match.Groups["rect"];

            if (transformGroup.Success && rectGroup.Success)
            {
                transformType = transformationsMap[transformGroup.Captures[0].Value];

                var ints = new int[4];
                var parts = rectGroup.Captures[0].Value.Split(',');
                for (int i = 0; i < 4; i++)
                {
                    if (!int.TryParse(parts[i], out ints[i]))
                    {
                        rect = new Rectangle();
                        return false;
                    }
                }
                rect = new Rectangle(ints[0], ints[1], ints[2], ints[3]);
                return true;
            }
            transformType = 0;
            rect = new Rectangle();
            return false;
        }
        
        

        private bool ValidateImage(HttpListenerRequest request, out Image image)
        {
            if (request.ContentLength64 > 100 * 1024 * 1024)
            {
                image = null;
                return false;
            }
            
            if (request.InputStream == null)
            {
                image = null;
                return false;
            }
            var data = request.InputStream.ToArray();
            try
            {
                var img = Image.FromStream(new MemoryStream(data));
                image = null;
                if (img.Width > 1000 || img.Height > 1000)
                    return false;
                if (img.PixelFormat != PixelFormat.Format32bppArgb)
                    return false;

                image = img;
                return true;
            }
            catch
            {
                image = null;
                return false;
            }
        }

        private static Regex _matcher = new Regex(@"^\/process\/(?'transformType'rotate-cw|rotate-ccw|flip-v|flip-h)\/(?'rect'(?:-{0,1}\d{1,}\,){3}-{0,1}\d{1,})$", RegexOptions.Compiled);
        private static readonly Dictionary<string, TransformType> transformationsMap = new Dictionary<string, TransformType>()
        {
            {"rotate-cw", TransformType.RotateCw},
            {"rotate-ccw", TransformType.RotateCCW},
            {"flip-v", TransformType.FlipV},
            {"flip-h", TransformType.FlipH}
        };
        private readonly HttpListener listener;

        private Thread listenerThread;
        private bool disposed;
        private volatile bool isRunning;
    }
}