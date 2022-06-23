using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BackendGUI.Enumeration;
using Camstar.WCF.ObjectStack;
using ComponentFactory.Krypton.Toolkit;
using OpcenterWikLibrary;
using MesData;

namespace BackendGUI
{
    public partial class MainForm : KryptonForm
    {

        #region CONSTRUCTOR
        public MainForm()
        {
            InitializeComponent();
            Rectangle r = new Rectangle(0, 0, Pb_IndicatorPicture.Width, Pb_IndicatorPicture.Height);
            System.Drawing.Drawing2D.GraphicsPath gp = new System.Drawing.Drawing2D.GraphicsPath();
            int d = 28;
            gp.AddArc(r.X, r.Y, d, d, 180, 90);
            gp.AddArc(r.X + r.Width - d, r.Y, d, d, 270, 90);
            gp.AddArc(r.X + r.Width - d, r.Y + r.Height - d, d, d, 0, 90);
            gp.AddArc(r.X, r.Y + r.Height - d, d, d, 90, 90);
            Pb_IndicatorPicture.Region = new Region(gp);

            _mesData = new Mes("Backend Minime",AppSettings.Resource);

          
          

            WindowState = FormWindowState.Normal;
            Size = new Size(820, 716);
            MyTitle.Text = $"Backend - {AppSettings.Resource}";
            ResourceGrouping.Values.Heading = $"Resource Status: {AppSettings.Resource}";
            ResourceDataGroup.Values.Heading = $"Resource Data Collection: {AppSettings.Resource}";
        }
        #endregion

        #region INSTANCE VARIABLE

        private static Mes _mesData;
        private static DateTime _dMoveIn;
        private BackEndState _backEndState;

        #endregion
        private async Task SetBackendState(BackEndState backEndState)
        {
            _backEndState = backEndState;
            switch (_backEndState)
            {
                case BackEndState.PlaceUnit:
                    Tb_Scanner.Enabled = false;
                    lblCommand.ForeColor = Color.Red;
                    lblCommand.Text = "Resource is not in \"Up\" condition!";
                    break;
                case BackEndState.ScanUnitSerialNumber:
                    Tb_Operation.Clear();
                    Tb_PO.Clear();
                    Tb_ContainerPosition.Clear();
                    Tb_CartonSerialNumber.Clear();
                    Tb_ColorBoxSerialNumber.Clear();
                    Tb_SerialNumber.Clear();
                    Lb_MaterialList.Items.Clear();
                    GroupofMaterial.Values.Heading = @"Material -";

                    if (_mesData.ResourceStatusDetails == null || _mesData.ResourceStatusDetails?.Availability != "Up")
                    {
                        await SetBackendState(BackEndState.PlaceUnit);
                        break;
                    }

                    Tb_Scanner.Enabled = true;
                    lblCommand.ForeColor = Color.LimeGreen;
                    lblCommand.Text = @"Scan Unit Serial Number!";
                    ActiveControl = Tb_Scanner;
                    break;
                case BackEndState.CheckUnitStatus:
                    Tb_Scanner.Enabled = false;
                    lblCommand.Text = @"Checking Unit Status";

                    var oContainerStatus = await Mes.GetContainerStatusDetails(_mesData, Tb_SerialNumber.Text, _mesData.DataCollectionName);
                    Tb_ContainerPosition.Text = await Mes.GetCurrentContainerStep(_mesData, Tb_SerialNumber.Text);
                    if (oContainerStatus != null)
                    {
                        if (oContainerStatus.MfgOrderName != null) Tb_PO.Text = oContainerStatus.MfgOrderName.ToString();
                        if (oContainerStatus.Operation != null) Tb_Operation.Text = oContainerStatus.Operation.Name;
                        if (oContainerStatus.Operation?.Name != _mesData.DataCollectionName)
                        {
                            await SetBackendState(BackEndState.WrongPosition);
                            break;
                        }
                        GroupofMaterial.Values.Heading = $@"Material: {oContainerStatus.ContainerName.Value}";
                        if (oContainerStatus.MfgOrderName?.ToString() != "")
                        {
                            var mfgOrder = await Mes.GetMfgOrder(_mesData, oContainerStatus.MfgOrderName?.ToString());
                            if (mfgOrder != null)
                            {
                                if (mfgOrder.MaterialList.Length > 0)
                                {
                                    Lb_MaterialList.Items.Add($"Material | Qty");
                                    foreach (var materialItem in mfgOrder.MaterialList)
                                    {
                                        if (materialItem.QtyRequired != 0 && materialItem.RouteStep != null) if (materialItem.RouteStep.ToString().Split('{')[0] == oContainerStatus.OperationName.Value) Lb_MaterialList.Items.Add($"{materialItem.Product.Name} | {materialItem.QtyRequired} | {materialItem.RouteStep.ToString().Split('{')[0]}");
                                    }
                                }
                            }

                        }

                        _dMoveIn = DateTime.Now;
                        await SetBackendState(BackEndState.ScanColorBoxSerialNumber);
                        break;
                    }
                    await SetBackendState(BackEndState.UnitNotFound);
                    break;
                case BackEndState.UnitNotFound:
                    Tb_Scanner.Enabled = false;
                    lblCommand.ForeColor = Color.Red;
                    lblCommand.Text = @"Unit Not Found";
                    break;
                case BackEndState.ScanProductSerialNumber:
                    Tb_Scanner.Enabled = true;
                    lblCommand.Text = @"Scan Product Serial Number!";
                    break;
                case BackEndState.ScanColorBoxSerialNumber:
                    Tb_Scanner.Enabled = true;
                    lblCommand.Text = @"Scan Color Box Serial Number!";
                    break;
                case BackEndState.ScanCartonBoxSerialNumber:
                    Tb_Scanner.Enabled = true;
                    lblCommand.Text = @"Scan Carton Box Serial Number!";
                    break;
                case BackEndState.ScanLabelSerialNumber:
                    Tb_Scanner.Enabled = true;
                    lblCommand.Text = @"Scan Label Serial Number!";
                    break;
                case BackEndState.UpdateMoveInMove:
                    Tb_Scanner.Enabled = false;
                    lblCommand.Text = @"Container Move In";
                    var cDataPoint = new DataPointDetails[2];
                   
                    cDataPoint[0] = new DataPointDetails { DataName = "Color Box Serial Number", DataValue = Tb_ColorBoxSerialNumber.Text, DataType = DataTypeEnum.String };
                    cDataPoint[1] = new DataPointDetails { DataName = "Carton Box Serial Number", DataValue = Tb_CartonSerialNumber.Text, DataType = DataTypeEnum.String };
                    
                    oContainerStatus = await Mes.GetContainerStatusDetails(_mesData, Tb_SerialNumber.Text, _mesData.DataCollectionName);
                    if (oContainerStatus != null)
                    {
                        var resultMoveIn = await Mes.ExecuteMoveIn(_mesData, oContainerStatus.ContainerName.Value,
                            _dMoveIn, 15000);
                        if (resultMoveIn.Result)
                        {
                            lblCommand.Text = @"Container Move Standard";
                            var resultMoveStd = await Mes.ExecuteMoveStandard(_mesData, oContainerStatus.ContainerName.Value, DateTime.Now, cDataPoint, 30000);
                            await SetBackendState(resultMoveStd.Result
                                ? BackEndState.ScanUnitSerialNumber
                                : BackEndState.MoveInOkMoveFail);
                            break;
                        }

                        await SetBackendState(BackEndState.MoveInFail);
                    }
                    break;
                case BackEndState.MoveInOkMoveFail:
                    lblCommand.ForeColor = Color.Red;
                    Tb_Scanner.Enabled = false;
                    lblCommand.Text = @"Move Standard Fail";
                    break;
                case BackEndState.MoveInFail:
                    lblCommand.ForeColor = Color.Red;
                    Tb_Scanner.Enabled = false;
                    lblCommand.Text = @"Move In Fail";
                    break;
                case BackEndState.Done:
                    break;
                case BackEndState.WrongPosition:
                    lblCommand.ForeColor = Color.Red;
                    Tb_Scanner.Enabled = false;
                    lblCommand.Text = @"Incorrect Product Operation";
                    break;
            }
        }

        #region FUNCTION STATUS OF RESOURCE

        private async Task GetStatusMaintenanceDetails()
        {
            try
            {
                 var maintenanceStatusDetails = await Mes.GetMaintenanceStatusDetails(_mesData);
                if (maintenanceStatusDetails != null)
                {
                    Dg_Maintenance.DataSource = maintenanceStatusDetails;
                    Dg_Maintenance.Columns["Due"].Visible = false;
                    Dg_Maintenance.Columns["Warning"].Visible = false;
                    Dg_Maintenance.Columns["PastDue"].Visible = false;
                    Dg_Maintenance.Columns["MaintenanceReqName"].Visible = false;
                    Dg_Maintenance.Columns["MaintenanceReqDisplayName"].Visible = false;
                    Dg_Maintenance.Columns["ResourceStatusCodeName"].Visible = false;
                    Dg_Maintenance.Columns["UOMName"].Visible = false;
                    Dg_Maintenance.Columns["ResourceName"].Visible = false;
                    Dg_Maintenance.Columns["UOM2Name"].Visible = false;
                    Dg_Maintenance.Columns["MaintenanceReqRev"].Visible = false;
                    Dg_Maintenance.Columns["NextThruputQty2Warning"].Visible = false;
                    Dg_Maintenance.Columns["NextThruputQty2Limit"].Visible = false;
                    Dg_Maintenance.Columns["UOM2"].Visible = false;
                    Dg_Maintenance.Columns["ThruputQty2"].Visible = false;
                    Dg_Maintenance.Columns["Resource"].Visible = false;
                    Dg_Maintenance.Columns["ResourceStatusCode"].Visible = false;
                    Dg_Maintenance.Columns["NextThruputQty2Due"].Visible = false;
                    Dg_Maintenance.Columns["MaintenanceClassName"].Visible = false;
                    Dg_Maintenance.Columns["MaintenanceStatus"].Visible = false;
                    Dg_Maintenance.Columns["ExportImportKey"].Visible = false;
                    Dg_Maintenance.Columns["DisplayName"].Visible = false;
                    Dg_Maintenance.Columns["Self"].Visible = false;
                    Dg_Maintenance.Columns["IsEmpty"].Visible = false;
                    Dg_Maintenance.Columns["FieldAction"].Visible = false;
                    Dg_Maintenance.Columns["IgnoreTypeDifference"].Visible = false;
                    Dg_Maintenance.Columns["ListItemAction"].Visible = false;
                    Dg_Maintenance.Columns["ListItemIndex"].Visible = false;
                    Dg_Maintenance.Columns["CDOTypeName"].Visible = false;
                    Dg_Maintenance.Columns["key"].Visible = false;
                }
            }
            catch (Exception ex)
            {
                ex.Source = AppSettings.AssemblyName == ex.Source ? MethodBase.GetCurrentMethod()?.Name : MethodBase.GetCurrentMethod()?.Name + "." + ex.Source;
                EventLogUtil.LogErrorEvent(ex.Source, ex);
            }
        }
        private async Task GetStatusOfResource(int timeOut=5000)
        {
            try
            {
                var resourceStatus = await Mes.GetResourceStatusDetails(_mesData,timeOut);
                _mesData.SetResourceStatusDetails(resourceStatus);

                if (resourceStatus != null)
                {
                    if (resourceStatus.Status != null) Tb_StatusCode.Text = resourceStatus.Status.Name;
                    if (resourceStatus.Reason != null) Tb_StatusReason.Text = resourceStatus.Reason.Name;
                    if (resourceStatus.Availability != null)
                    {
                        Tb_Availability.Text = resourceStatus.Availability.Value;
                        if (resourceStatus.Availability.Value == "Up")
                        {
                            Pb_IndicatorPicture.BackColor = Color.Green;
                        }
                        else if (resourceStatus.Availability.Value == "Down")
                        {
                            Pb_IndicatorPicture.BackColor = Color.Red;
                        }
                    }
                    else
                    {
                        Pb_IndicatorPicture.BackColor = Color.Orange;
                    }
                   
                    if (resourceStatus.TimeAtStatus != null)
                        Tb_TimeAtStatus.Text =
                            $@"{DateTime.FromOADate(resourceStatus.TimeAtStatus.Value) - Mes.ZeroEpoch():G}";
                }
            }
            catch (Exception ex)
            {
                ex.Source = AppSettings.AssemblyName == ex.Source ? MethodBase.GetCurrentMethod()?.Name : MethodBase.GetCurrentMethod()?.Name + "." + ex.Source;
                EventLogUtil.LogErrorEvent(ex.Source, ex);
            }
        }
        #endregion

        #region COMPONENT EVENT
       
       
        
        private async void TimerRealtime_Tick(object sender, EventArgs e)
        {
            await GetStatusOfResource();
            await GetStatusMaintenanceDetails();
        }
       
       

     
        private async void btnResetState_Click(object sender, EventArgs e)
        {
            await SetBackendState(BackEndState.ScanUnitSerialNumber);
        }

        private async  void Tb_Scanner_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            if (string.IsNullOrEmpty(Tb_Scanner.Text)) return;
            switch (_backEndState)
            {
                case BackEndState.ScanUnitSerialNumber:
                    Tb_SerialNumber.Text = Tb_Scanner.Text;
                    await SetBackendState(BackEndState.CheckUnitStatus);
                    break;
                case BackEndState.ScanColorBoxSerialNumber:
                    Tb_ColorBoxSerialNumber.Text = Tb_Scanner.Text;
                    Tb_Scanner.Clear();
                    await SetBackendState(BackEndState.ScanCartonBoxSerialNumber);
                    break;
                case BackEndState.ScanCartonBoxSerialNumber:
                    Tb_CartonSerialNumber.Text = Tb_Scanner.Text;
                    Tb_Scanner.Clear();
                    await SetBackendState(BackEndState.UpdateMoveInMove);
                    break;
                
            }
            Tb_Scanner.Clear();
        }
        #endregion

        private async void Main_Load(object sender, EventArgs e)
        {
            await GetStatusOfResource();
            await GetStatusMaintenanceDetails();
            await SetBackendState(BackEndState.ScanUnitSerialNumber);
        }

        private async void btnResourceSetup_Click(object sender, EventArgs e)
        {
            Mes.ResourceSetupForm(this, _mesData, MyTitle.Text);
            await GetStatusOfResource();
        }
    }
}
