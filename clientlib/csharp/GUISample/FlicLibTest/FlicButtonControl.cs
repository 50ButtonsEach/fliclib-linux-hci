using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using FliclibDotNetClient;

namespace FlicLibTest
{
    public partial class FlicButtonControl : UserControl
    {
        public bool Listens
        {
            get;
            set;
        }

        public ButtonConnectionChannel Channel
        {
            get;
            set;
        }

        public FlicButtonControl()
        {
            InitializeComponent();
        }
    }
}
