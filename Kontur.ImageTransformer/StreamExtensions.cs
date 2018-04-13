using System;
using System.IO;


namespace Kontur.ImageTransformer
{
    public static class StreamExtensions
    {
        public static byte[] ToArray(this Stream input)
        {
            if (input == null) throw new ArgumentNullException("input");

            var stream = input as MemoryStream;
            if (stream != null)
            {
                return stream.ToArray();
            }
            else
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    input.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }
    }
}
