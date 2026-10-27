using System;
using System.Text;

namespace ZeroReports.Thermal
{
    /// <summary>
    /// High-performance Zebra Programming Language II (ZPL II) label encoder.
    /// Produces pure ASCII printer control streams for industrial barcode/thermal printers.
    /// </summary>
    public class ZplEncoder
    {
        private readonly StringBuilder _sb = new StringBuilder();

        public ZplEncoder()
        {
            StartLabel();
        }

        public ZplEncoder StartLabel()
        {
            _sb.AppendLine("^XA");
            return this;
        }

        public ZplEncoder EndLabel()
        {
            _sb.AppendLine("^XZ");
            return this;
        }

        public ZplEncoder SetDarkness(int darkness)
        {
            if (darkness >= 0 && darkness <= 30)
            {
                _sb.AppendLine($"~SD{darkness:D2}");
            }
            return this;
        }

        public ZplEncoder SetPrintSpeed(int speedInchesPerSec)
        {
            _sb.AppendLine($"^PR{speedInchesPerSec}");
            return this;
        }

        public ZplEncoder DrawText(int x, int y, string text, int fontHeight = 28, int fontWidth = 28, string font = "0")
        {
            if (string.IsNullOrEmpty(text)) return this;
            _sb.AppendLine($"^FO{x},{y}^A{font}N,{fontHeight},{fontWidth}^FD{Escape(text)}^FS");
            return this;
        }

        public ZplEncoder DrawBox(int x, int y, int width, int height, int borderThickness = 2)
        {
            _sb.AppendLine($"^FO{x},{y}^GB{width},{height},{borderThickness}^FS");
            return this;
        }

        public ZplEncoder DrawLine(int x, int y, int length, int thickness = 2, bool isHorizontal = true)
        {
            if (isHorizontal)
                _sb.AppendLine($"^FO{x},{y}^GB{length},{thickness},{thickness}^FS");
            else
                _sb.AppendLine($"^FO{x},{y}^GB{thickness},{length},{thickness}^FS");
            return this;
        }

        public ZplEncoder DrawBarcode128(int x, int y, string data, int height = 60, int moduleWidth = 2, bool showInterpretation = true)
        {
            if (string.IsNullOrEmpty(data)) return this;
            string printText = showInterpretation ? "Y" : "N";
            _sb.AppendLine($"^FO{x},{y}^BY{moduleWidth}^BCN,{height},{printText},N,N^FD{Escape(data)}^FS");
            return this;
        }

        public ZplEncoder DrawDataMatrix(int x, int y, string data, int moduleSize = 5)
        {
            if (string.IsNullOrEmpty(data)) return this;
            _sb.AppendLine($"^FO{x},{y}^BXN,{moduleSize},200,,,,1^FD{Escape(data)}^FS");
            return this;
        }

        public ZplEncoder DrawBarcode39(int x, int y, string data, int height = 60, int moduleWidth = 2, bool showInterpretation = true)
        {
            if (string.IsNullOrEmpty(data)) return this;
            string printText = showInterpretation ? "Y" : "N";
            _sb.AppendLine($"^FO{x},{y}^BY{moduleWidth}^B3N,N,{height},{printText},N^FD{Escape(data)}^FS");
            return this;
        }

        public ZplEncoder DrawEan13(int x, int y, string data, int height = 60, int moduleWidth = 2, bool showInterpretation = true)
        {
            if (string.IsNullOrEmpty(data)) return this;
            string printText = showInterpretation ? "Y" : "N";
            _sb.AppendLine($"^FO{x},{y}^BY{moduleWidth}^BEN,{height},{printText},N^FD{Escape(data)}^FS");
            return this;
        }

        public ZplEncoder DrawTextRotated(int x, int y, string text, char orientation = 'R', int fontHeight = 28, int fontWidth = 28, string font = "0")
        {
            if (string.IsNullOrEmpty(text)) return this;
            _sb.AppendLine($"^FO{x},{y}^A{font}{orientation},{fontHeight},{fontWidth}^FD{Escape(text)}^FS");
            return this;
        }

        public ZplEncoder DrawFieldInvert(int x, int y, int width, int height)
        {
            _sb.AppendLine($"^FO{x},{y}^FR^GB{width},{height},{height}^FS");
            return this;
        }

        public ZplEncoder DrawQrCode(int x, int y, string data, int magnification = 4)
        {
            if (string.IsNullOrEmpty(data)) return this;
            _sb.AppendLine($"^FO{x},{y}^BQN,2,{magnification}^FDQA,{Escape(data)}^FS");
            return this;
        }

        private static string Escape(string input)
        {
            // Escape special ZPL characters like _ or ^ if needed
            return input.Replace("^", "_5E");
        }

        public string ToZplString() => _sb.ToString();
        public byte[] ToByteArray() => Encoding.ASCII.GetBytes(_sb.ToString());

        public override string ToString() => ToZplString();
    }

    /// <summary>
    /// TSC Printer Language (TSPL) command generator for TSC and compatible thermal transfer printers.
    /// </summary>
    public class TsplEncoder
    {
        private readonly StringBuilder _sb = new StringBuilder();

        public TsplEncoder(int widthMm = 100, int heightMm = 75)
        {
            _sb.AppendLine($"SIZE {widthMm} mm, {heightMm} mm");
            _sb.AppendLine("GAP 3 mm, 0 mm");
            _sb.AppendLine("DIRECTION 1");
            _sb.AppendLine("CLS");
        }

        public TsplEncoder Clear()
        {
            _sb.AppendLine("CLS");
            return this;
        }

        public TsplEncoder DrawText(int x, int y, string text, string font = "3", int xMulti = 1, int yMulti = 1)
        {
            if (string.IsNullOrEmpty(text)) return this;
            _sb.AppendLine($"TEXT {x},{y},\"{font}\",0,{xMulti},{yMulti},\"{text}\"");
            return this;
        }

        public TsplEncoder DrawBox(int x, int y, int xEnd, int yEnd, int lineThickness = 2)
        {
            _sb.AppendLine($"BOX {x},{y},{xEnd},{yEnd},{lineThickness}");
            return this;
        }

        public TsplEncoder DrawBarcode(int x, int y, string data, string type = "128", int height = 60, int readable = 1)
        {
            if (string.IsNullOrEmpty(data)) return this;
            _sb.AppendLine($"BARCODE {x},{y},\"{type}\",{height},{readable},0,2,2,\"{data}\"");
            return this;
        }

        public TsplEncoder DrawBarcodeEan(int x, int y, string data, int height = 60)
        {
            return DrawBarcode(x, y, data, type: "EAN13", height: height);
        }

        public TsplEncoder DrawDataMatrix(int x, int y, string data, int cellWidth = 6)
        {
            if (string.IsNullOrEmpty(data)) return this;
            _sb.AppendLine($"DMATRIX {x},{y},100,100,c{cellWidth},\"{data}\"");
            return this;
        }

        public TsplEncoder DrawQrCode(int x, int y, string data, int cellWidth = 4)
        {
            if (string.IsNullOrEmpty(data)) return this;
            _sb.AppendLine($"QRCODE {x},{y},L,{cellWidth},A,0,\"{data}\"");
            return this;
        }

        public TsplEncoder DrawReverse(int x, int y, int width, int height)
        {
            _sb.AppendLine($"REVERSE {x},{y},{width},{height}");
            return this;
        }

        public TsplEncoder Print(int copies = 1)
        {
            _sb.AppendLine($"PRINT {copies}");
            return this;
        }

        public string ToTsplString() => _sb.ToString();
        public byte[] ToByteArray() => Encoding.ASCII.GetBytes(_sb.ToString());

        public override string ToString() => ToTsplString();
    }
}
