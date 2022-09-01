using System.ComponentModel;
using MesData;
using MesData.Settings;

namespace BackendGUI
{
    public class BackendComponentSetting : AppSettings<BackendComponentSetting>
    {
        [Category("Enable / Disable Config"), Browsable(true), ReadOnly(false), DefaultValue(EnableDisable.Enable), DesignOnly(false),
         DescriptionAttribute("Carton Box Barcode Record"), DisplayName("Carton Box Barcode Record")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public EnableDisable ColorBoxEnable { get; set; } = EnableDisable.Enable;


        [Category("Enable / Disable Config"), Browsable(true), ReadOnly(false), DefaultValue(EnableDisable.Enable), DesignOnly(false),
         DescriptionAttribute("Master Box Barcode Record"), DisplayName("Master Box Barcode Record")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public EnableDisable MasterCartonEnable { get; set; } = EnableDisable.Enable;


        [Category("Enable / Disable Config"), Browsable(true), ReadOnly(false), DefaultValue(EnableDisable.Enable), DesignOnly(false),
         DescriptionAttribute("Label Record"), DisplayName("Label Record")]
       [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public EnableDisable LabelEnable { get; set; } = EnableDisable.Enable;


        [Category("Enable / Disable Config"), Browsable(false), ReadOnly(false), DefaultValue(EnableDisable.Enable),
         DesignOnly(false),
         DescriptionAttribute("Label Record")]
        public const string FileName = "Component.json";

        public void SaveFile()
        {
            Save(FileName);
        }
    }
}