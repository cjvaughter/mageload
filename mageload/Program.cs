using System;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using mageload.Properties;

namespace mageload
{
    public enum Commands : byte
    {
        OK        = 0xF0,
        Fail      = 0xFF,
        Execute   = 0x0A,
        Signature = 0x01,
        Address   = 0x02,
        Read      = 0x03,
        Write     = 0x04,
        Exit      = 0x05
    }

    public enum Signature : byte
    {
        PIU_0 = 0x1E,
        PIU_1 = 0x98,
        PIU_2 = 0x01,
        Cert_0 = 0x1E,
        Cert_1 = 0x95,
        Cert_2 = 0x0F,
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

    class Program
    {
        static bool _verbose;
        //static bool _ota; //Next Version
        static string _port = "";
        static string _file = "";
        static string _hex = "";
        static ushort _pageSize;
        static byte[] _data;
        static uint _maxAddress;
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
                             + "       -o OTA mode (no verification)\n\n"
                             + "-p port:  Specify port (stored for reuse)\n"
                             + "          If no port specified, a window will appear\n\n"
                             + "-f file:  The hex file to load\n\n";

        static void Main(string[] args)
        {
            if (!ParseArgs(args)) return;
            if (!LoadFile()) return;
            if (!InitializeSerial()) return;
            if (!ParseFile()) return;
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
            Console.Write("\nUpload successful\n");
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
            if (args[argIndex].ToLower() == "-o")
            {
                //_ota = true; //Next Version
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
                    Settings.Default.Port = _port;
                    Settings.Default.Save();
                }
                else
                {
                    _port = args[argIndex];
                    argIndex++;
                    Settings.Default.Port = _port;
                    Settings.Default.Save();
                }
            }
            if (argIndex < args.Length)
            {
                if (args[argIndex].ToLower() == "-f")
                {
                    _file = args[++argIndex];
                    //argIndex++;
                }
            }
            if (_file == "" && _port == "")
            {
                Console.Write(Usage);
                return false;
            }

            if (_file == "") return false;
            if (_port == "") _port = Settings.Default.Port; _port = Settings.Default.Port;

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
            catch (Exception e)
            {
                Console.Write("\nThe file could not be read\n\n");
                if (_verbose) Console.WriteLine(e.Message);
                return false;
            }
            return true;
        }

        static bool ParseFile()
        {
            uint baseAddress = 0;
            bool eofFound = false;
            string[] records = _hex.Split(':');
            try
            {
                foreach (string r in records)
                {
                    if (r.Length == 0) continue;
                    byte length = Convert.ToByte(r.Substring(0, 2), 16);
                    uint address = Convert.ToUInt16(r.Substring(2, 4), 16) + baseAddress;
                    RecordType type = (RecordType)Convert.ToByte(r.Substring(6, 2), 16);
                    string data = r.Substring(8, length * 2);

                    switch (type)
                    {
                        case RecordType.Data:
                            char[] digits = data.ToCharArray();
                            for (int i = 0; i < digits.Length; i += 2)
                            {
                                _data[address+i/2] = Convert.ToByte(String.Concat(digits[i], digits[i + 1]), 16);
                            }
                            uint temp = address + length;
                            if (temp > _maxAddress) _maxAddress = temp;
                            break;
                        case RecordType.EOF:
                            eofFound = true;
                            break;
                        case RecordType.ExtSegAddress:
                            baseAddress = Convert.ToUInt16(data, 16);
                            baseAddress *= 16;
                            break;
                        case RecordType.StartSegAddress:
                            //Nothing to do here
                            break;
                        case RecordType.ExtLinAddress:
                        case RecordType.StartLinAddress:
                            Console.Write("\nUnknown record type in file\n\n");
                            if (_verbose) Console.WriteLine("Type: " + type);
                            break;
                    }
                }
            }
            catch(Exception e)
            {
                Console.Write("\nUnrecognized file\n\n");
                if (_verbose) Console.WriteLine(e.Message);
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

            _serial = new SerialPort(_port, 115200, Parity.None, 8, StopBits.One);
            _serial.ReadTimeout = 500;
            _serial.WriteTimeout = 500;

            try
            {
                _serial.Open();
                _serial.DtrEnable = true;
                Thread.Sleep(50);
                _serial.DtrEnable = false;
                Thread.Sleep(50);
            }
            catch (Exception e)
            {
                Console.Write("\nCould not open COM port\n\n");
                if(_verbose) Console.WriteLine(e.Message);
                return false;
            }

            try
            {
                byte[] outbfr = {(byte) Commands.Signature, (byte) Commands.Execute};
                byte[] inbfr = new byte[4];
                _serial.ReadExisting();
                _serial.Write(outbfr, 0, 2);

                inbfr[0] = (byte) _serial.ReadByte();
                inbfr[1] = (byte) _serial.ReadByte();
                inbfr[2] = (byte) _serial.ReadByte();
                inbfr[3] = (byte) _serial.ReadByte();

                if (inbfr[0] == (byte) Signature.PIU_0 && inbfr[1] == (byte) Signature.PIU_1 &&
                    inbfr[2] == (byte) Signature.PIU_2 && inbfr[3] == (byte) Commands.OK)
                {
                    _pageSize = 256;
                    if (_verbose) Console.WriteLine("Player Interface Unit found\n");
                    _data = Enumerable.Repeat<byte>(0xFF, 261120).ToArray(); //leave space for bootloader
                }
                else if (inbfr[0] == (byte) Signature.Cert_0 && inbfr[1] == (byte) Signature.Cert_1 &&
                         inbfr[2] == (byte) Signature.Cert_2 && inbfr[3] == (byte) Commands.OK)
                {
                    _pageSize = 128;
                    if (_verbose) Console.WriteLine("MCU Certification Board found\n");
                    _data = Enumerable.Repeat<byte>(0xFF, 32000).ToArray();
                }
                else
                {
                    Console.Write("\nInvalid device detected\n\n");
                    if (_verbose)
                        Console.WriteLine(inbfr[0].ToString("X") + " " + inbfr[1].ToString("X") + " " + inbfr[2].ToString("X"));
                    return false;
                }
            }
            catch (Exception e)
            {
                Console.Write("\nCould read device signature\n\n");
                if (_verbose) Console.WriteLine(e.Message);
                return false;
            }

            return true;
        }

        static bool SendData()
        {
            try
            {
                if (!_verbose) Console.Write("Writing   ");
                _serial.ReadExisting();
                uint lastpercent = 0;
                for (uint i = 0; i < _maxAddress; i += _pageSize)
                {
                    if (!_verbose)
                    {
                        uint percent = (uint) (((float) i/(float) _maxAddress)*50);
                        if (percent >= lastpercent)
                        {
                            for (; lastpercent <= percent; lastpercent++)
                                Console.Write("+");
                        }
                    }
                    byte[] bfr = new byte[6];
                    bfr[0] = (byte)Commands.Address;
                    uint address = i / 2;
                    bfr[1] = (byte)(address >> 24);
                    bfr[2] = (byte)(address >> 16);
                    bfr[3] = (byte)(address >> 8);
                    bfr[4] = (byte)(address);
                    bfr[5] = (byte)Commands.Execute;
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
                    bfr[0] = (byte)Commands.Write;
                    bfr[1] = (byte)(size >> 8);
                    bfr[2] = (byte)(size);
                    bfr[3] = (byte)(Commands.Execute);
                    if (_verbose) Console.Write("WRITE   " + ToHex(bfr) + " ");
                    _serial.Write(bfr, 0, 4);
                    if (_verbose) Console.Write("...DATA... ");
                    _serial.Write(_data, (int)i, _pageSize);
                    while (_serial.BytesToWrite != 0) {}

                    response = (byte)_serial.ReadByte();
                    if (_verbose) Console.WriteLine(ToHex(response));
                    if (response != (byte)Commands.OK)
                    {
                        Console.Write("\nFailed to write memory\n\n");
                        if (_verbose) Console.WriteLine("Invalid response: 0x" + response.ToString("X"));
                        return false;
                    }
                }
                if (!_verbose) Console.Write("\n\n");
            }
            catch (Exception e)
            {
                Console.Write("\nCommunication failure\n\n");
                if (_verbose) Console.WriteLine(e.Message);
                return false;
            }
            return true;
        }

        static bool VerifyData()
        {
            try
            {
                if (!_verbose) Console.Write("Verifying ");
                byte[] bfr;
                uint lastpercent = 0;
                for (uint i = 0; i < _maxAddress; i += _pageSize)
                {
                    if (!_verbose)
                    {
                        uint percent = (uint)(((float)i / (float)_maxAddress) * 50);
                        if (percent >= lastpercent)
                        {
                            for (; lastpercent <= percent; lastpercent++)
                                Console.Write("+");
                        }
                    }
                    bfr = new byte[6];
                    bfr[0] = (byte)Commands.Address;
                    uint address = i / 2;
                    bfr[1] = (byte)(address >> 24);
                    bfr[2] = (byte)(address >> 16);
                    bfr[3] = (byte)(address >> 8);
                    bfr[4] = (byte)(address);
                    bfr[5] = (byte)Commands.Execute;
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
                    bfr[0] = (byte)Commands.Read;
                    bfr[1] = (byte)(size >> 8);
                    bfr[2] = (byte)(size);
                    bfr[3] = (byte)(Commands.Execute);
                    if (_verbose) Console.Write("READ    " + ToHex(bfr) + " ");
                    _serial.Write(bfr, 0, 4);

                    byte[] data = new byte[_pageSize];
                    if (_verbose) Console.Write("...DATA... ");
                    for (int j = 0; j < _pageSize; j++)
                    {
                        data[j] = (byte)_serial.ReadByte();
                    }

                    response = (byte)_serial.ReadByte();
                    if (_verbose) Console.WriteLine(ToHex(response));
                    if (response != (byte)Commands.OK)
                    {
                        Console.Write("\nFailed to read memory\n\n");
                        if (_verbose) Console.WriteLine("Invalid response: 0x" + response.ToString("X"));
                        return false;
                    }

                    for (int j = 0; j < _pageSize; j++)
                    {
                        if (_data[i + j] != data[j])
                        {
                            Console.Write("\nFailed to verify memory\n\n");
                            if (_verbose) Console.WriteLine("Address: " + i.ToString("X") + "\nRead: " + _data[i+j].ToString("X") + " Expected: " + data[j].ToString("X"));
                            return false;
                        }
                    }
                }

                if (!_verbose) Console.Write("\n");
                bfr = new byte[2];
                bfr[0] = (byte)Commands.Exit;
                bfr[1] = (byte)(Commands.Execute);
                if (_verbose) Console.WriteLine("EXIT");
                _serial.Write(bfr, 0, 2);

                SerialClose();
            }
            catch (Exception e)
            {
                Console.Write("\nCommunication failure\n\n");
                if (_verbose) Console.WriteLine(e.Message);
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
            catch (Exception e)
            {
                Console.Write("\nCould not close " + _port + "\n\n");
                if(_verbose) Console.WriteLine(e.Message);
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
