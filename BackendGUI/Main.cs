using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;
using MesData;
using MesData.Login;
using OpcenterWikLibrary;
using BackendGUI.Enumeration;
using MesData.Backend;

namespace BackendGUI
{
    public partial class Main : KryptonForm
    {
        #region CONSTRUCTOR
        public Main()
        {
            InitializeComponent();

#if MiniMe
            var  name = "Backend Minime";
            Text = Mes.AddVersionNumber(name);
#elif Ariel
            var name = "Backend Ariel";
            Text = Mes.AddVersionNumber(name);
#endif
            _mesData = new Mes("", AppSettings.Resource,name);

            WindowState = FormWindowState.Normal;
            Size = new Size(1134, 701);
            lbTitle.Text =AppSettings.Resource;


            _componentSetting = BackendComponentSetting.Load();
            _componentSetting?.Save();

            kryptonNavigator1.SelectedIndex = 0;
            EventLogUtil.LogEvent("Application Start");
        }

        public sealed override string Text
        {
            get => base.Text;
            set => base.Text = value;
        }

        #endregion

#region INSTANCE VARIABLE
      
        private BackEndState _backEndState;
        private readonly Mes _mesData;
        private DateTime _dMoveIn;
        private BackendComponentSetting _componentSetting;
        private BackendComponentSetting _tempComponentSetting;

        #endregion

#region FUNCTION USEFULL
        
        private async Task SetBackendState(BackEndState backEndState)
        {
            _backEndState = backEndState;
            switch (_backEndState)
            {
                case BackEndState.PlaceUnit:
                    _readScanner = false;
                    break;
                case BackEndState.ScanUnitSerialNumber:
                    _readScanner = true;
                    lblCommand.ForeColor = Color.LimeGreen;
                    lblCommand.Text = @"Scan Unit Serial Number!";
                    ClrContainer();
                    break;
                case BackEndState.CheckUnitStatus:
                    _readScanner = false;
                    lblCommand.Text = @"Checking Unit Status";
                    var oContainerStatus = await Mes.GetContainerStatusDetails(_mesData, Tb_SerialNumber.Text, _mesData.DataCollectionName);
                    if (oContainerStatus != null)
                    {
                       if (oContainerStatus.Operation?.Name != _mesData.OperationName)
                       {
                           await SetBackendState(BackEndState.WrongPosition);
                           break;
                       }
                       if (oContainerStatus.MfgOrderName?.ToString() != "" && _mesData.ManufacturingOrder==null)
                       {
                           var mfgOrder = await Mes.GetMfgOrder(_mesData, oContainerStatus.MfgOrderName?.ToString());
                           _mesData.SetManufacturingOrder(mfgOrder);
                           if (mfgOrder != null)
                           {
                               _backendComponent = new BackendComponent
                               {
                                   MasterCarton =
                                   {
                                       Enabled = mfgOrder.MaterialList.Where(x=>
                                           x.Product.Name.IndexOf("801", StringComparison.Ordinal) == 0 ||
                                           x.Product.Name.IndexOf("820", StringComparison.Ordinal) == 0 && x.wikScanning?.Value=="X" && x.wikScanning!=null).ToList().Count > 0

                                   },
                                   ColorBox =
                                   {
                                       Enabled = mfgOrder.MaterialList.Where(x =>
                                           x.Product.Name.IndexOf("802", StringComparison.Ordinal) == 0 && x.wikScanning?.Value=="X" && x.wikScanning!=null).ToList().Count > 0
                                   },
                                   ColorBoxLabel =
                                   {
                                         Enabled = mfgOrder.MaterialList.Where(x =>
                                           x.Product.Name.IndexOf("809", StringComparison.Ordinal) == 0 && x.wikScanning?.Value=="X" && x.wikScanning!=null).ToList().Count > 0
                                   },
                                   MasterCartonLabel =
                                   {
                                           Enabled = mfgOrder.MaterialList.Where(x =>
                                           x.Product.Name.IndexOf("809", StringComparison.Ordinal) == 0 && x.wikScanning?.Value=="X" && x.wikScanning!=null).ToList().Count > 1
                                        },
                               };

                               _backendComponent.MasterCarton.Enabled &= _componentSetting.MasterCartonEnable == EnableDisable.Enable;
                               _backendComponent.ColorBox.Enabled &= _componentSetting.ColorBoxEnable == EnableDisable.Enable;
                               _backendComponent.MasterCartonLabel.Enabled &= _componentSetting.LabelEnable == EnableDisable.Enable;
                               _backendComponent.ColorBoxLabel.Enabled &= _componentSetting.LabelEnable == EnableDisable.Enable;

                               lblColorBox.Visible = _backendComponent.ColorBox.Enabled;
                               lblColorBoxLabel.Visible = _backendComponent.ColorBoxLabel.Enabled;
                               lblMasterCarton.Visible = _backendComponent.MasterCarton.Enabled;
                               lblMasterCartonLabel.Visible = _backendComponent.MasterCartonLabel.Enabled;

                               Tb_MasterCarton.Visible = _backendComponent.MasterCarton.Enabled;
                               Tb_ColorBox.Visible = _backendComponent.ColorBox.Enabled;
                               Tb_MasterCartonLabel.Visible = _backendComponent.MasterCartonLabel.Enabled;
                               Tb_ColorBoxLabel.Visible = _backendComponent.ColorBoxLabel.Enabled;

                               // Update PO information
                               Tb_PO.Text = oContainerStatus.MfgOrderName?.Value;
                               Tb_Product.Text = oContainerStatus.Product.Name;
                               Tb_ProductDesc.Text = oContainerStatus.ProductDescription.Value;

                               var img = await Mes.GetImage(_mesData, oContainerStatus.Product.Name);
                               pictureBox1.ImageLocation = img.Identifier.Value;

                               var count = await Mes.GetCounterFromMfgOrder(_mesData);
                               Tb_PpaQty.Text = count.ToString();
                           }
                            
                       }

                       _dMoveIn = DateTime.Now;
                       lbMoveIn.Text = _dMoveIn.ToString(Mes.DateTimeStringFormat);
                       lbMoveOut.Text = "";
                        _backendComponent.ResetValue();
                        if (_backendComponent.Completed)
                        {
                            await SetBackendState(BackEndState.UpdateMoveInMove);
                            break;
                        }
                       await SetBackendState(BackEndState.ScanAny);
                       break;
                    }
                    await SetBackendState(BackEndState.UnitNotFound);
                    break;
                case BackEndState.UnitNotFound:
                    _readScanner = false;
                    lblCommand.ForeColor = Color.Red;
                    lblCommand.Text = @"Unit Not Found";
                    break;
                case BackEndState.ScanAny:
                    _readScanner = true;
                    lblCommand.ForeColor = Color.LimeGreen;
                    lblCommand.Text = @"Scan Any Component!";
                    break;
                case BackEndState.UpdateMoveInMove:
                    _readScanner = false;
                    lblCommand.ForeColor = Color.LimeGreen;
                    lblCommand.Text = @"Moving Container...";
                    oContainerStatus = await Mes.GetContainerStatusDetails(_mesData, Tb_SerialNumber.Text, _mesData.DataCollectionName);
                    if (oContainerStatus != null)
                    {
                        var resultMoveIn = await Mes.ExecuteMoveIn(_mesData, oContainerStatus.ContainerName.Value,
                            _dMoveIn, 15000);
                        if (resultMoveIn.Result)
                        {
                            //Consume Component
                            lblCommand.Text = @"Component Consume.";
                            var listIssue = new List<dynamic>();
                            if (_backendComponent.MasterCarton.Enabled) listIssue.Add(_backendComponent.MasterCarton.ToIssueActualDetail());
                            if (_backendComponent.MasterCartonLabel.Enabled) listIssue.Add(_backendComponent.MasterCartonLabel.ToIssueActualDetail());
                            if (_backendComponent.ColorBox.Enabled) listIssue.Add(_backendComponent.ColorBox.ToIssueActualDetail());
                            if (_backendComponent.ColorBoxLabel.Enabled) listIssue.Add(_backendComponent.ColorBoxLabel.ToIssueActualDetail());

                            if (listIssue.Count > 0)
                            {
                                var transIssue = await Mes.ExecuteComponentIssue(_mesData, oContainerStatus.ContainerName.Value,
                                    listIssue);
                                if (!transIssue.Result)
                                {
                                    await SetBackendState(BackEndState.ComponentIssueFailed);
                                    break;
                                }
                            }

                            lblCommand.Text = @"Container Move Standard";
                            _dMoveOut = DateTime.Now;
                            lbMoveOut.Text = _dMoveOut.ToString(Mes.DateTimeStringFormat);
                            var resultMoveStd = await Mes.ExecuteMoveStandard(_mesData, oContainerStatus.ContainerName.Value, _dMoveOut, null, 30000);
                            await SetBackendState(resultMoveStd.Result
                                ? BackEndState.ScanUnitSerialNumber
                                : BackEndState.MoveInOkMoveFail);

                            //Update Counter
                            if (resultMoveStd.Result)
                            {

                                var mfgOrder = await Mes.GetMfgOrder(_mesData, _mesData.ManufacturingOrder.Name?.ToString());
                                _mesData.SetManufacturingOrder(mfgOrder);
                                await Mes.UpdateCounter(_mesData,1);
                                var count = await Mes.GetCounterFromMfgOrder(_mesData);
                                Tb_PpaQty.Text = count.ToString();
                            }
                            
                            break;
                        }
                        // check if fail by maintenance Past Due
                        var transPastDue = Mes.GetMaintenancePastDue(_mesData.MaintenanceStatusDetails);
                        if (transPastDue.Result)
                        {
                            KryptonMessageBox.Show(this, "This resource under maintenance, need to complete!", "Move In",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        await SetBackendState(BackEndState.MoveInFail);
                    }
                    break;
                case BackEndState.MoveSuccess:
                    _readScanner = false;
                    break;
                case BackEndState.MoveInOkMoveFail:
                    _readScanner = false;
                    lblCommand.ForeColor = Color.Red;
                    lblCommand.Text = @"Move Standard Fail";
                    break;
                case BackEndState.MoveInFail:
                    lblCommand.ForeColor = Color.Red;
                    lblCommand.Text = @"Move In Fail";
                    _readScanner = false;
                    break;
                case BackEndState.Done:
                    _readScanner = false;
                    break;
                case BackEndState.WrongPosition:
                    _readScanner = false;
                    lblCommand.ForeColor = Color.Red;
                    lblCommand.Text = @"Wrong product position";
                    break;
                case BackEndState.WrongComponent:
                    _readScanner = false;
                    lblCommand.ForeColor = Color.Red;
                    lblCommand.Text = @"Wrong Component";
                    break;
                case BackEndState.WaitPreparation:
                    _readScanner = false;
                    lblCommand.ForeColor = Color.Red;
                    lblCommand.Text = @"Wait Preparation";
                    btnStartPreparation.Enabled = true;
                    break;
                case BackEndState.ComponentNotFound:
                    lblCommand.ForeColor = Color.Red;
                    _readScanner = false;
                    lblCommand.Text = @"Component Not Found";
                    break;
                case BackEndState.ComponentIssueFailed:
                    lblCommand.ForeColor = Color.Red;
                    _readScanner = false;
                    lblCommand.Text = @"Component Issue Failed.";
                    break;
            }
        }

        private void ClrContainer()
        {
            Tb_Scanner.Clear();
            Tb_SerialNumber.Clear();
            Tb_ColorBox.Clear();
            Tb_ColorBoxLabel.Clear();
            Tb_MasterCarton.Clear();
            Tb_MasterCartonLabel.Clear();
        }

        #endregion

#region FUNCTION STATUS OF RESOURCE

        private async Task GetStatusMaintenanceDetails()
        {
            try
            {
                var maintenanceStatusDetails = await Mes.GetMaintenanceStatusDetails(_mesData);
                if (maintenanceStatusDetails != null)
                {
                    _mesData.SetMaintenanceStatusDetails(maintenanceStatusDetails);
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
        private async Task GetStatusOfResource()
        {
            try
            {
                var resourceStatus = await Mes.GetResourceStatusDetails(_mesData);
                _mesData.SetResourceStatusDetails(resourceStatus);

                if (resourceStatus != null)
                {
                    if (resourceStatus.Status != null) Tb_StatusCode.Text = resourceStatus.Reason?.Name;
                    if (resourceStatus.Availability != null)
                    {
                        if (resourceStatus.Availability.Value == "Up")
                        {
                            Tb_StatusCode.StateCommon.Content.Color1 = resourceStatus.Reason?.Name == "Quality Inspection" ? Color.Orange : Color.Green;
                        }
                        else if (resourceStatus.Availability.Value == "Down")
                        {
                            Tb_StatusCode.StateCommon.Content.Color1 = Color.Red;
                        }
                    }
                    else
                    {
                        Tb_StatusCode.StateCommon.Content.Color1 = Color.Orange;
                    }

                    if (resourceStatus.TimeAtStatus != null)
                        Tb_TimeAtStatus.Text = $@"{Mes.OaTimeSpanToString(resourceStatus.TimeAtStatus.Value)}";
                }
            }
            catch (Exception ex)
            {
                ex.Source = AppSettings.AssemblyName == ex.Source ? MethodBase.GetCurrentMethod()?.Name : MethodBase.GetCurrentMethod()?.Name + "." + ex.Source;
                EventLogUtil.LogErrorEvent(ex.Source, ex);
            }
        }

        private async Task GetStatusOfResourceDetail()
        {
            try
            {
                var resourceStatus = await Mes.GetResourceStatusDetails(_mesData);
                _mesData.SetResourceStatusDetails(resourceStatus);

                if (resourceStatus != null)
                {
                    if (resourceStatus.Status != null) Cb_StatusCode.Text = resourceStatus.Status.Name;
                    await Task.Delay(1000);
                    if (resourceStatus.Reason != null) Cb_StatusReason.Text = resourceStatus.Reason.Name;
                    if (resourceStatus.Availability != null)
                    {
                        Tb_StatusCodeM.Text = resourceStatus.Availability.Value;
                        if (resourceStatus.Availability.Value == "Up")
                        {
                            Tb_StatusCodeM.StateCommon.Content.Color1 = Color.Green;
                        }
                        else if (resourceStatus.Availability.Value == "Down")
                        {
                            Tb_StatusCodeM.StateCommon.Content.Color1 = Color.Red;
                        }
                    }
                    else
                    {
                        Tb_StatusCodeM.StateCommon.Content.Color1 = Color.Orange;
                    }

                    if (resourceStatus.TimeAtStatus != null)
                        Tb_TimeAtStatus.Text = $@"{Mes.OaTimeSpanToString(resourceStatus.TimeAtStatus.Value)}";
                }
            }
            catch (Exception ex)
            {
                ex.Source = AppSettings.AssemblyName == ex.Source
                    ? MethodBase.GetCurrentMethod()?.Name
                    : MethodBase.GetCurrentMethod()?.Name + "." + ex.Source;
                EventLogUtil.LogErrorEvent(ex.Source, ex);
            }
        }

        private async Task GetResourceStatusCodeList()
        {
            try
            {
                var oStatusCodeList = await Mes.GetListResourceStatusCode(_mesData);
                if (oStatusCodeList != null)
                {
                    Cb_StatusCode.DataSource = oStatusCodeList.Where(x=>x.Name.IndexOf("BE", StringComparison.Ordinal)==0).ToList();
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
            if (_backEndState == BackEndState.WaitPreparation) return;
            await SetBackendState(BackEndState.ScanUnitSerialNumber);
            Tb_Scanner.Focus();
        }

        private bool _readScanner;
        private bool _ignoreScanner;
        private BackendComponent _backendComponent;
        private DateTime _dMoveOut;

        private async void Tb_Scanner_KeyUp(object sender, KeyEventArgs e)
        {
            if(!_readScanner) Tb_Scanner.Clear();
            if (_ignoreScanner) e.Handled = true;
            if (e.KeyCode == Keys.Enter)
            {
                _ignoreScanner = true;
                if (!string.IsNullOrEmpty(Tb_Scanner.Text))
                {
                    switch (_backEndState)
                    {
                        case BackEndState.ScanUnitSerialNumber:
                            Tb_SerialNumber.Text = Tb_Scanner.Text.Trim();
                            Tb_Scanner.Clear();
                            await SetBackendState(BackEndState.CheckUnitStatus);
                            break;
                        case BackEndState.ScanAny:
                            if (_backendComponent.Completed)
                            {
                                await SetBackendState(BackEndState.UpdateMoveInMove);
                                break;
                            }
                            var scanned = Tb_Scanner.Text.Trim();
                            //validate
                            if (scanned.Length >= 10)
                            {
                                var valid = _mesData.ManufacturingOrder.MaterialList.Where(x =>
                                    x.Product.Name.IndexOf(scanned.Substring(0, 10), StringComparison.Ordinal)==0).ToList();
                                if (valid.Count>0)
                                {
                                    var distinct = scanned.Substring(0, 3);
                                    //distinguish component
                                    
                                        if (distinct == "801" || distinct == "820")
                                        {
                                            if (_backendComponent.MasterCarton.Enabled)
                                            {
                                                _backendComponent.MasterCarton.Value = scanned;
                                                _backendComponent.MasterCarton.QuantityRequired =
                                                    valid[0].QtyRequired.Value;
                                                Tb_MasterCarton.Text = scanned;
                                                if (valid[0].wikScanning != "X" || valid[0].wikScanning==null)
                                                {
                                                    await SetBackendState(BackEndState.WrongComponent);
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                await SetBackendState(BackEndState.WrongComponent);
                                                break;
                                            }
                                    }
                                    
                                    if (distinct == "802")
                                    {
                                        if (_backendComponent.ColorBox.Enabled)
                                        {
                                            _backendComponent.ColorBox.Value = scanned;
                                            _backendComponent.ColorBox.QuantityRequired = valid[0].QtyRequired.Value;
                                            Tb_ColorBox.Text = scanned;
                                            if (valid[0].wikScanning != "X" || valid[0].wikScanning == null)
                                            {
                                                await SetBackendState(BackEndState.WrongComponent);
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            await SetBackendState(BackEndState.WrongComponent);
                                            break;
                                        }
                                    }
                                    
                                    if (distinct == "809" && _backendComponent.ColorBoxLabel.Value == null)
                                    {
                                        if (_backendComponent.ColorBoxLabel.Enabled)
                                        {
                                            _backendComponent.ColorBoxLabel.Value = scanned;
                                            _backendComponent.ColorBoxLabel.QuantityRequired = valid[0].QtyRequired.Value;
                                            Tb_ColorBoxLabel.Text = scanned;
                                            if (valid[0].wikScanning != "X" || valid[0].wikScanning == null)
                                            {
                                                await SetBackendState(BackEndState.WrongComponent);
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            await SetBackendState(BackEndState.WrongComponent);
                                            break;
                                        }
                                    }
                                    else
                                    {

                                        if (distinct == "809" && _backendComponent.MasterCartonLabel.Value == null 
                                                              && !_backendComponent.ColorBoxLabel.Value.Equals(scanned))
                                        {
                                            if (_backendComponent.MasterCartonLabel.Enabled)
                                            {
                                                _backendComponent.MasterCartonLabel.Value = scanned;
                                                _backendComponent.MasterCartonLabel.QuantityRequired =
                                                    valid[0].QtyRequired.Value;
                                                Tb_MasterCartonLabel.Text = scanned;
                                                if (valid[0].wikScanning != "X" || valid[0].wikScanning == null)
                                                {
                                                    await SetBackendState(BackEndState.WrongComponent);
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                await SetBackendState(BackEndState.WrongComponent);
                                                break;
                                            }
                                        }
                                    }

                                    if (distinct != "809" && distinct != "802" &&
                                        !(distinct == "801" || distinct == "820"))
                                    {
                                        await SetBackendState(BackEndState.WrongComponent);
                                        break;
                                    }
                                }
                                else
                                {
                                    await SetBackendState(BackEndState.ComponentNotFound);
                                    break;
                                }
                            }
                            if (_backendComponent.Completed)
                            {
                                 await SetBackendState(BackEndState.UpdateMoveInMove);
                            }
                            break;
                    }
                }

                _ignoreScanner = false;
                Tb_Scanner.Clear();
            }
        }
#endregion

        private async void Main_Load(object sender, EventArgs e)
        {
            await GetStatusOfResource();
            await GetStatusMaintenanceDetails();
            await GetResourceStatusCodeList();
            await SetBackendState(BackEndState.WaitPreparation);
        }

        private void ClearPo()
        {
            Tb_PO.Clear();
            Tb_Product.Clear();
            Tb_ProductDesc.Clear();
            Tb_PpaQty.Clear();
            Tb_FinishedGoodCounter.Clear();
            pictureBox1.ImageLocation = null;

            lblColorBox.Visible = false;
            lblColorBoxLabel.Visible = false;
            lblMasterCarton.Visible = false;
            lblMasterCartonLabel.Visible = false;

            Tb_ColorBox.Visible = false;
            Tb_ColorBoxLabel.Visible = false;
            Tb_MasterCarton.Visible = false;
            Tb_MasterCartonLabel.Visible = false;

            ClrContainer();
        }
      
        private void kryptonGroupBox2_Panel_Paint(object sender, PaintEventArgs e)
        {

        }

        private async void Cb_StatusCode_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                var oStatusCode = await Mes.GetResourceStatusCode(_mesData, Cb_StatusCode.SelectedValue != null ? Cb_StatusCode.SelectedValue.ToString() : "");
                if (oStatusCode != null)
                {
                    Tb_StatusCodeM.Text = oStatusCode.Availability.ToString();
                    if (oStatusCode.ResourceStatusReasons != null)
                    {
                        var oStatusReason = await Mes.GetResourceStatusReasonGroup(_mesData, oStatusCode.ResourceStatusReasons.Name);
                        Cb_StatusReason.DataSource = oStatusReason.Entries;
                    }
                    else
                    {
                        Cb_StatusReason.Items.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                ex.Source = AppSettings.AssemblyName == ex.Source ? MethodBase.GetCurrentMethod()?.Name : MethodBase.GetCurrentMethod()?.Name + "." + ex.Source;
                EventLogUtil.LogErrorEvent(ex.Source, ex);
            }
        }
        private async void btnSetMachineStatus_Click(object sender, EventArgs e)
        {
            try
            {
                var result = false;
                if (Cb_StatusCode.Text != "" && Cb_StatusReason.Text != "")
                {
                    result = await Mes.SetResourceStatus(_mesData, Cb_StatusCode.Text, Cb_StatusReason.Text);
                }
                else if (Cb_StatusCode.Text != "")
                {
                    result = await Mes.SetResourceStatus(_mesData, Cb_StatusCode.Text, "");
                }

                await GetStatusOfResourceDetail();
                await GetStatusOfResource();
                KryptonMessageBox.Show(result ? "Setup status successful" : "Setup status failed");

            }
            catch (Exception ex)
            {
                ex.Source = AppSettings.AssemblyName == ex.Source ? MethodBase.GetCurrentMethod()?.Name : MethodBase.GetCurrentMethod()?.Name + "." + ex.Source;
                EventLogUtil.LogErrorEvent(ex.Source, ex);
            }
        }

        private async void kryptonNavigator1_SelectedPageChanged(object sender, EventArgs e)
        {
            if (kryptonNavigator1.SelectedIndex == 1)
            {
                await GetStatusOfResourceDetail();
            }
            if (kryptonNavigator1.SelectedIndex == 2)
            {
                _tempComponentSetting = BackendComponentSetting.Load(BackendComponentSetting.FileName);
                Ppg_Pcba.SelectedObject = _tempComponentSetting;
            }
            if (kryptonNavigator1.SelectedIndex == 3)
            {
                lblPo.Text = $@"Serial Number of PO: {_mesData.ManufacturingOrder?.Name}";
                lblLoading.Visible = true;
                await GetFinishedGoodRecord();
                lblLoading.Visible = false;
            }

        }
        private async Task GetFinishedGoodRecord()
        {
            var data = await Mes.GetFinishGoodRecord(_mesData, _mesData.ManufacturingOrder?.Name.ToString());
            if (data != null)
            {
                var list = await Mes.ContainerStatusesToFinishedGood(data);
                finishedGoodBindingSource.DataSource = new BindingList<FinishedGood>(list);
                kryptonDataGridView1.DataSource = finishedGoodBindingSource;
                Tb_FinishedGoodCounter.Text = list.Length.ToString();
            }
        }
        private void Btn_SetPcba_Click(object sender, EventArgs e)
        {
            _tempComponentSetting.SaveFile();
            _componentSetting = BackendComponentSetting.Load(BackendComponentSetting.FileName);
        }

       
        private void kryptonNavigator1_Selecting(object sender, ComponentFactory.Krypton.Navigator.KryptonPageCancelEventArgs e)
        {
            if (e.Index != 1 && e.Index != 2) return;

            using (var ss = new LoginForm24())
            {
                var dlg = ss.ShowDialog(this);
                if (dlg == DialogResult.Abort)
                {
                    KryptonMessageBox.Show("Login Failed");
                    e.Cancel = true;
                    return;
                }
                if (dlg == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
                if (ss.UserDetails.UserRole == UserRole.Maintenance && e.Index != 1) e.Cancel = true;
                if (ss.UserDetails.UserRole == UserRole.Quality && e.Index != 2) e.Cancel = true;
            }


        }

        private async void btnCallMaintenance_Click(object sender, EventArgs e)
        {
            try
            {
                var result = await Mes.SetResourceStatus(_mesData, "BE - Internal Downtime", "Maintenance");
                await GetStatusOfResource();
                KryptonMessageBox.Show(result ? "Setup status successful" : "Setup status failed");

            }
            catch (Exception ex)
            {
                ex.Source = AppSettings.AssemblyName == ex.Source ? MethodBase.GetCurrentMethod()?.Name : MethodBase.GetCurrentMethod()?.Name + "." + ex.Source;
                EventLogUtil.LogErrorEvent(ex.Source, ex);
            }
        }

        private async void btnFinishPreparation_Click(object sender, EventArgs e)
        {
            if (_mesData.ResourceStatusDetails.Reason.Name == "Maintenance") return;
            var result = await Mes.SetResourceStatus(_mesData, "BE - Productive Time", "Pass");
            await GetStatusOfResource();
            if (result)
            {
                btnFinishPreparation.Enabled = false;
                btnStartPreparation.Enabled = true;
                await SetBackendState(BackEndState.ScanUnitSerialNumber);
            }
        }

        private async void btnStartPreparation_Click(object sender, EventArgs e)
        {
            ClearPo();
            if (_mesData.ResourceStatusDetails?.Reason?.Name=="Maintenance") return;
            _mesData.SetManufacturingOrder(null);
            var result = await Mes.SetResourceStatus(_mesData, "BE - Planned Downtime", "Preparation");
            await GetStatusOfResource();
            if (result)
            {
                await SetBackendState(BackEndState.WaitPreparation);
                btnFinishPreparation.Enabled = true;
                btnStartPreparation.Enabled = false;
            }
        }

       
        private void Tb_Scanner_TextChanged(object sender, EventArgs e)
        {

        }

        private void Dg_Maintenance_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            try
            {
                foreach (DataGridViewRow row in Dg_Maintenance.Rows)
                {
                    //Console.WriteLine(Convert.ToString(row.Cells["MaintenanceState"].Value));
                    if (Convert.ToString(row.Cells["MaintenanceState"].Value) == "Pending")
                    {
                        row.DefaultCellStyle.BackColor = Color.Yellow;
                    }
                    else if (Convert.ToString(row.Cells["MaintenanceState"].Value) == "Due")
                    {
                        row.DefaultCellStyle.BackColor = Color.Orange;
                    }
                    else if (Convert.ToString(row.Cells["MaintenanceState"].Value) == "Past Due")
                    {
                        row.DefaultCellStyle.BackColor = Color.Red;
                    }
                }
            }
            catch (Exception ex)
            {
                ex.Source = AppSettings.AssemblyName == ex.Source ? MethodBase.GetCurrentMethod()?.Name : MethodBase.GetCurrentMethod()?.Name + "." + ex.Source;
                EventLogUtil.LogErrorEvent(ex.Source, ex);
            }
        }
    }
}
