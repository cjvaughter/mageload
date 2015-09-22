using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;

namespace mageload
{
    public partial class PortWindow : Form
    {
        string _port = "";
        public PortWindow()
        {
            InitializeComponent();
            string[] ports = SerialPort.GetPortNames();
            foreach (string p in ports)
            {
                cbPort.Items.Add(p);
                if (Properties.Settings.Default.Port == p)
                    cbPort.Text = p;
            }

        }
        public string getPort()
        {
            return _port;
        }

        private void cbPort_SelectedIndexChanged(object sender, EventArgs e)
        {
            _port = cbPort.GetItemText(cbPort.SelectedItem);
        }
    }
}
