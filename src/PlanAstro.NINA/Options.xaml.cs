using System.ComponentModel.Composition;
using System.Windows;

namespace PlanAstro.NINA {

    /// <summary>Registers the settings page template (Options.xaml) with NINA.</summary>
    [Export(typeof(ResourceDictionary))]
    public partial class Options : ResourceDictionary {
        public Options() {
            InitializeComponent();
        }
    }
}
