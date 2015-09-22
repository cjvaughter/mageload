using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace mageload
{
    public enum Commands : byte
    {
        OK        = 0xF0,
        FAIL      = 0xFF,
        EXECUTE   = 0x0A,
        SIGNATURE = 0x01,
        ADDRESS   = 0x02,
        READ      = 0x03,
        WRITE     = 0x04,
        EXIT      = 0x05
    }

    public enum Signature : byte
    {
        atmega2560_0 = 0x1E,
        atmega2560_1 = 0x98,
        atmega2560_2 = 0x01,
        atmega328p_0 = 0x1E,
        atmega328p_1 = 0x95,
        atmega328p_2 = 0x0F,
    }

    public enum RecordType : byte
    {
        Data,
        EOF,
        ExtSegAddress,
        StartSegAddress,
        ExtLinAddress,
        StartLinAddress
    }

    public class FlashPage
    {
        public FlashPage(uint address = 0)
        {
            Address = address;
            Data = Enumerable.Repeat<byte>(0xFF, 256).ToArray();
        }
        public uint Address;
        public byte[] Data;
    }

    class Program
    {
        static bool _verbose;
        static string _port = "";
        static string _file = "";
        static string _hex = "";
        static ushort _pageSize = 0;
        static List<FlashPage> _pages = new List<FlashPage>();
        static SerialPort _serial;
        const string Usage = "\n ███╗   ███╗ █████╗  ██████╗ ███████╗██╗      ██████╗  █████╗ ██████╗\n"
                             + " ████╗ ████║██╔══██╗██╔════╝ ██╔════╝██║     ██╔═══██╗██╔══██╗██╔══██╗\n"
                             + " ██╔████╔██║███████║██║  ███╗█████╗  ██║     ██║   ██║███████║██║  ██║\n"
                             + " ██║╚██╔╝██║██╔══██║██║   ██║██╔══╝  ██║     ██║   ██║██╔══██║██║  ██║\n"
                             + " ██║ ╚═╝ ██║██║  ██║╚██████╔╝███████╗███████╗╚██████╔╝██║  ██║██████╔╝\n"
                             + " ╚═╝     ╚═╝╚═╝  ╚═╝ ╚═════╝ ╚══════╝╚══════╝ ╚═════╝ ╚═╝  ╚═╝╚═════╝\n"
                             + "--------------------------Application Loader--------------------------\n\n"
                             + "Usage: mageload [options] [-p port] [-f file]\n\n"
                             + "OPTIONS:\n"
                             + "       -v Verbose mode\n\n"
                             + "-p port:  Specify port (stored for reuse)\n"
                             + "          If no port specified, a window will appear\n\n"
                             + "-f file:  The hex file to load\n\n";

        static void Main(string[] args)
        {
            if (!ParseArgs(args)) return;
            if (!LoadFile()) return;
            if (!ParseFile()) return;
            if (!InitializeSerial()) return;
            if (!SendData())
            {
                SerialClose();
                return;
            }
            if (!VerifyData())
            {
                SerialClose();
                return;
            }
            Console.Write("\nUpload successful\n\n");
        }

        static bool ParseArgs(string[] args)
        {
            int argIndex = 0;
            if (args.Length == 0)
            {
                Console.Write(Usage);
                return false;
            }
            if (args[argIndex].ToLower() == "-v")
            {
                _verbose = true;
                argIndex++;
            }
            if (args[argIndex].ToLower() == "-p")
            {
                argIndex++;
                if (argIndex == args.Length)
                {
                    PortWindow window = new PortWindow();
                    DialogResult r = window.ShowDialog();
                    if (r != DialogResult.OK) return false;
                    _port = window.getPort();
                    Properties.Settings.Default.Port = _port;
                    Properties.Settings.Default.Save();
                }
                else
                {
                    _port = args[argIndex];
                    argIndex++;
                    Properties.Settings.Default.Port = _port;
                    Properties.Settings.Default.Save();
                }
            }
            if (argIndex < args.Length)
            {
                if (args[argIndex].ToLower() == "-f")
                {
                    _file = args[++argIndex];
                    argIndex++;
                }
            }
            if (_file == "" && _port == "")
            {
                Console.Write(Usage);
                return false;
            }

            if (_file == "") return false;
            if (_port == "") _port = Properties.Settings.Default.Port; _port = Properties.Settings.Default.Port;

            return true;
        }

        static bool LoadFile()
        {
            if (!File.Exists(_file))
            {
                Console.Write("\nFile does not exist: " + _file + "\n\n");
                return false;
            }
            try
            {
                using (StreamReader s = new StreamReader(_file))
                {
                    _hex = s.ReadToEnd();
                }
            }
            catch (Exception)
            {
                Console.Write("\nThe file could not be read\n\n");
                return false;
            }
            return true;
        }

        static bool ParseFile()
        {
            uint baseAddress = 0;
            FlashPage page = new FlashPage();
            short bytecount = 0;
            bool eofFound = false;
            string[] records = _hex.Split(':');
            try
            {
                foreach (string r in records)
                {
                    if (r.Length == 0) continue;
                    byte length = Convert.ToByte(r.Substring(0, 2), 16);
                    ushort address = Convert.ToUInt16(r.Substring(2, 4), 16);
                    RecordType type = (RecordType)Convert.ToByte(r.Substring(6, 2), 16);
                    string data = r.Substring(8, length * 2);

                    switch (type)
                    {
                        case RecordType.Data:
                            char[] digits = data.ToCharArray();
                            for (int i = 0; i < digits.Length; i += 2)
                            {
                                if (bytecount == 0) page.Address += address;
                                string value = String.Concat(digits[i], digits[i + 1]);
                                page.Data[bytecount++] = Convert.ToByte(value, 16);
                                if (bytecount == _pageSize)
                                {
                                    bytecount = 0;
                                    _pages.Add(page);
                                    page = new FlashPage(baseAddress);
                                }
                            }
                            break;
                        case RecordType.EOF:
                            eofFound = true;
                            break;
                        case RecordType.ExtSegAddress:
                            if (bytecount != 0)
                            {
                                bytecount = 0;
                                _pages.Add(page);
                                page = new FlashPage();
                            }
                            baseAddress = Convert.ToUInt16(data, 16);
                            baseAddress *= 16;
                            page.Address = baseAddress;
                            break;
                        case RecordType.StartSegAddress:
                            //Nothing to do here
                            break;
                        case RecordType.ExtLinAddress:
                        case RecordType.StartLinAddress:
                            Console.Write("\nUnknown record type in file\n\n");
                            break;
                    }
                }

                if (bytecount > 0) _pages.Add(page);
            }
            catch(Exception)
            {
                Console.Write("\nUnrecognized file\n\n");
                return false;
            }

            return eofFound;
        }

        static bool InitializeSerial()
        {
            if (!SerialPort.GetPortNames().Contains(_port))
            {
                Console.Write("\nInvalid COM port: " + _port + "\n\n");
                return false;
            }

            _serial = new SerialPort(_port, 38400, Parity.None, 8, StopBits.One);
            _serial.ReadTimeout = 500;
            _serial.WriteTimeout = 500;

            try
            {
                _serial.Open();
                _serial.DtrEnable = true;
                System.Threading.Thread.Sleep(50);
                _serial.DtrEnable = false;
                System.Threading.Thread.Sleep(50);
            }
            catch (Exception)
            {
                Console.Write("\nCould not open COM port\n\n");
                return false;
            }

            byte[] bfr = new byte[4] {(byte)Commands.SIGNATURE, (byte)Commands.EXECUTE, 0, 0};
            _serial.Write(bfr, 0, 2);
            _serial.Read(bfr, 0, 4);

            if (bfr[0] == (byte)Signature.atmega2560_0 && bfr[1] == (byte)Signature.atmega2560_1 && bfr[2] == (byte)Signature.atmega2560_2 && bfr[3] == (byte)Commands.OK)
            {
                _pageSize = 256;
                if (_verbose) Console.WriteLine("ATmega2560 found");
            }
            if (bfr[0] == (byte)Signature.atmega328p_0 && bfr[1] == (byte)Signature.atmega328p_1 && bfr[2] == (byte)Signature.atmega328p_2 && bfr[3] == (byte)Commands.OK)
            {
                _pageSize = 128;
                if (_verbose) Console.WriteLine("ATmega328P found");
            }
            else
            {
                Console.Write("\nInvalid device detected\n\n");
                return false;
            }

            return true;
        }

        static bool SendData()
        {
            try
            {
                _serial.ReadExisting();
                foreach (FlashPage p in _pages)
                {
                    byte[] bfr = new byte[6];
                    bfr[0] = (byte)Commands.ADDRESS;
                    uint address = p.Address / 2;
                    bfr[1] = (byte)(address >> 24);
                    bfr[2] = (byte)(address >> 16);
                    bfr[3] = (byte)(address >> 8);
                    bfr[4] = (byte)(address);
                    bfr[5] = (byte)Commands.EXECUTE;
                    if (_verbose) Console.Write("ADDRESS " + ToHex(bfr) + " ");
                    _serial.Write(bfr, 0, 6);


                    byte response = (byte)_serial.ReadByte();
                    if (_verbose) Console.WriteLine(ToHex(response));
                    if (response != (byte)Commands.OK)
                    {
                        Console.Write("\nFailed to address memory\n\n");
                        if (_verbose) Console.WriteLine("Invalid response");
                        return false;
                    }

                    ushort size = _pageSize;
                    bfr = new byte[4];
                    bfr[0] = (byte)Commands.WRITE;
                    bfr[1] = (byte)(size >> 8);
                    bfr[2] = (byte)(size);
                    bfr[3] = (byte)(Commands.EXECUTE);
                    if (_verbose) Console.Write("WRITE   " + ToHex(bfr) + " ");
                    _serial.Write(bfr, 0, 4);
                    if (_verbose) Console.Write("...DATA... ");
                    _serial.Write(p.Data, 0, _pageSize);
                    while (_serial.BytesToWrite != 0) ;

                    response = (byte)_serial.ReadByte();
                    if (_verbose) Console.WriteLine(ToHex(response));
                    if (response != (byte)Commands.OK)
                    {
                        Console.Write("\nFailed to write memory\n\n");
                        if (_verbose) Console.WriteLine("Invalid response");
                        return false;
                    }
                }
            }
            catch (Exception)
            {
                Console.Write("\nCommunication failure\n\n");
                if (_verbose) Console.WriteLine("Port timed out");
                return false;
            }
            return true;
        }

        static bool VerifyData()
        {
            try
            {
                byte[] bfr;
                foreach (FlashPage p in _pages)
                {
                    bfr = new byte[6];
                    bfr[0] = (byte)Commands.ADDRESS;
                    uint address = p.Address / 2; //Not sure about this
                    bfr[1] = (byte)(address >> 24);
                    bfr[2] = (byte)(address >> 16);
                    bfr[3] = (byte)(address >> 8);
                    bfr[4] = (byte)(address);
                    bfr[5] = (byte)Commands.EXECUTE;
                    if (_verbose) Console.Write("ADDRESS " + ToHex(bfr) + " ");
                    _serial.Write(bfr, 0, 6);

                    byte response = (byte)_serial.ReadByte();
                    if (_verbose) Console.WriteLine(ToHex(response));
                    if (response != (byte)Commands.OK)
                    {
                        Console.Write("\nFailed to address memory\n\n");
                        if (_verbose) Console.WriteLine("Invalid response: 0x" + response.ToString("X"));
                        return false;
                    }

                    ushort size = _pageSize;
                    bfr = new byte[4];
                    bfr[0] = (byte)Commands.READ;
                    bfr[1] = (byte)(size >> 8);
                    bfr[2] = (byte)(size);
                    bfr[3] = (byte)(Commands.EXECUTE);
                    if (_verbose) Console.Write("READ    " + ToHex(bfr) + " ");
                    _serial.Write(bfr, 0, 4);

                    byte[] data = new byte[_pageSize];
                    if (_verbose) Console.Write("...DATA... ");
                    for (int i = 0; i < _pageSize; i++)
                    {
                        data[i] = (byte)_serial.ReadByte();
                    }

                    response = (byte)_serial.ReadByte();
                    if (_verbose) Console.WriteLine(ToHex(response));
                    if (response != (byte)Commands.OK)
                    {
                        Console.Write("\nFailed to read memory\n\n");
                        if (_verbose) Console.WriteLine("Invalid response");
                        return false;
                    }

                    for (int i = 0; i < _pageSize; i++)
                    {
                        if (p.Data[i] != data[i])
                        {
                            Console.Write("\nFailed to verify memory\n\n");
                            return false;
                        }
                    }
                }

                bfr = new byte[2];
                bfr[0] = (byte)Commands.EXIT;
                bfr[1] = (byte)(Commands.EXECUTE);
                if (_verbose) Console.WriteLine("EXIT");
                _serial.Write(bfr, 0, 2);

                SerialClose();
            }
            catch (Exception)
            {
                Console.Write("\nCommunication failure\n\n");
                if (_verbose) Console.WriteLine("Port timed out");
                return false;
            }
            return true;
        }

        static void SerialClose()
        {
            try
            {
                _serial.Close();
            }
            catch (Exception)
            {
                Console.Write("\nCould not close " + _port + "\n\n");
            }
        }

        static string ToHex(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        static string ToHex(byte ba)
        {
            StringBuilder hex = new StringBuilder(2);
            hex.AppendFormat("{0:x2}", ba);
            return hex.ToString();
        }
    }
}
