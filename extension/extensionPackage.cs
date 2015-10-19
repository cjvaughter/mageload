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
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace mageload.extension
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidList.guidextensionPkgString)]
    [ProvideAutoLoad("ADFC4E64-0397-11D1-9F4E-00A0C911004F")]
    public sealed class ExtensionPackage : Package, IDisposable
    {
        private DTE _dte;
        private BackgroundWorker _worker;

        public void Dispose()
        {
            _worker.Dispose();
        }

        protected override void Initialize()
        {
            base.Initialize();

            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs)
            {
                CommandID deployID = new CommandID(GuidList.guidextensionCmdSet, (int)PkgCmdIDList.cmdDeploy);
                OleMenuCommand deploy = new OleMenuCommand(Deploy, deployID);
                deploy.BeforeQueryStatus += button_BeforeQueryStatus;
                mcs.AddCommand(deploy);

                CommandID chooseportID = new CommandID(GuidList.guidextensionCmdSet, (int)PkgCmdIDList.cmdChoosePort);
                OleMenuCommand chooseport = new OleMenuCommand(ChoosePort, chooseportID);
                chooseport.BeforeQueryStatus += button_BeforeQueryStatus;
                mcs.AddCommand(chooseport);

                IVsOutputWindow outputWindow = GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
                if (outputWindow != null)
                {
                    IVsOutputWindowPane pane;
                    outputWindow.CreatePane(VSConstants.OutputWindowPaneGuid.GeneralPane_guid, "mageload", 1, 0);
                    outputWindow.GetPane(VSConstants.OutputWindowPaneGuid.GeneralPane_guid, out pane);
                    Program.Pane = pane;
                }
            }
            _worker = new BackgroundWorker();
            _worker.DoWork += DoDeploy;

            _dte = GetGlobalService(typeof(DTE)) as DTE;
            if (_dte != null) _dte.Commands.Item("Tools.Deploy").Bindings = "Global::F8";
        }

        private void Deploy(object sender, EventArgs e)
        {
            if (_worker.IsBusy) return;
            _worker.RunWorkerAsync();
        }

        private void DoDeploy(object sender, DoWorkEventArgs e)
        {
            Project proj = null;
            foreach (Project p in _dte.Solution.Projects)
            {
                proj = p;
            }
            if (proj == null) return;

            _dte.Solution.SolutionBuild.Build(true);
            if (_dte.Solution.SolutionBuild.LastBuildInfo != 0) return;

            string dir = proj.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputDirectory").Value.ToString();
            string file = proj.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputFile").Value.ToString();
            file = file.Replace(".elf", ".hex");

            Program.Main(new[] { "-f", dir + "\\" + file });
        }

        private void ChoosePort(object sender, EventArgs e)
        {
            if (_worker.IsBusy) return;
            Program.Main(new[] {"-p"});
        }

        private void button_BeforeQueryStatus(object sender, EventArgs e)
        {
            bool enabled = _dte.Solution.IsOpen;
            OleMenuCommand menuCommand = sender as OleMenuCommand;
            if (menuCommand != null)
            {
                menuCommand.Enabled = enabled;
            }
        }
    }
}
