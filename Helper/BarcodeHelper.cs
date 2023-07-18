using Barcoder.Aztec;
using Barcoder.Renderer.Image;
using Dto.Proxy.Response.Tosan;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PecBMS.Helper
{
    public static class BarcodeHelper
    {
        public static int[] GetCountTypeTicket(this List<SingleTicketResponseDto> list)
        {
            var first = list.Where(f => f.type == 0).Count();
            var second = list.Where(f => f.type == 1).Count();
            var thirth = list.Where(f => f.type == 2).Count();
            return new int[] { first, second, thirth };
        }
        public static string GetAztecQrCode(this string QrText)
        {
            var barcode = AztecEncoder.Encode(QrText);
            var renderer = new ImageRenderer(new ImageRendererOptions { ImageFormat = ImageFormat.Png });
            byte[] byteBarcode;
            using (var stream = new MemoryStream())
            {
                renderer.Render(barcode, stream);
                byteBarcode = stream.ToArray();
            }
            return Convert.ToBase64String(byteBarcode);
        }
    }
}
