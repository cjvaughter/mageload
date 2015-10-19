// Copyright 2015 Oklahoma State University
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.IO.Ports;
using System.Windows.Forms;
using mageload.Properties;

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
                if (Settings.Default.Port == p)
                    cbPort.Text = p;
            }

        }
        public string GetPort()
        {
            return _port;
        }

        private void cbPort_SelectedIndexChanged(object sender, EventArgs e)
        {
            _port = cbPort.GetItemText(cbPort.SelectedItem);
        }
    }
}
