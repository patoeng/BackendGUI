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
using Camstar.WCF.ObjectStack;
using MesData.Backend;
using MesData.UnitCounter;
using Environment = System.Environment;
using System.Linq.Dynamic;
using System.Windows.Forms.VisualStyles;

namespace BackendGUI
{
    public partial class Main : KryptonForm
    {
        #region CONSTRUCTOR

        public Main()
        {
            InitializeComponent();

#if MiniMe
            var name = "Backend Minime";
            Text = Mes.AddVersionNumber(name);
#elif Ariel
            var name = "Backend Ariel";
            Text = Mes.AddVersionNumber(name);
#endif
            _mesData = new Mes("Repair", AppSettings.Resource, name);

            lbTitle.Text = AppSettings.Resource;

            _syncWorker = new BackgroundWorker();
            _syncWorker.WorkerReportsProgress = true;
            _syncWorker.RunWorkerCompleted += SyncWorkerCompleted;
            _syncWorker.ProgressChanged += SyncWorkerProgress;
            _syncWorker.DoWork += SyncDoWork;

            _moveWorker = new AbortableBackgroundWorker();
            _moveWorker.WorkerReportsProgress = true;
            _moveWorker.RunWorkerCompleted += MoveWorkerCompleted;
            _moveWorker.ProgressChanged += MoveWorkerProgress;
            _moveWorker.DoWork += MoveWorkerDoWork;

            _componentSetting = BackendComponentSetting.Load(BackendComponentSetting.FileName);
            _componentSetting?.Save();

            kryptonNavigator1.SelectedIndex = 0;
            EventLogUtil.LogEvent("Application Start");

            //Prepare Maintenance Grid
            var maintStrings = new[]
            {
                "Resource", "MaintenanceType", "MaintenanceReq", "NextDateDue", "NextThruputQtyDue", "MaintenanceState"
            };

            for (int i = 0; i < Dg_Maintenance.Columns.Count; i++)
            {
                if (!maintStrings.Contains(Dg_Maintenance.Columns[i].DataPropertyName))
                {
                    Dg_Maintenance.Columns[i].Visible = false;
                }
                else
                {
                    switch (Dg_Maintenance.Columns[i].HeaderText)
                    {

                        case "MaintenanceType":
                            Dg_Maintenance.Columns[i].HeaderText = @"Maintenance Type";
                            break;
                        case "MaintenanceReq":
                            Dg_Maintenance.Columns[i].HeaderText = @"Maintenance Requirement";
                            break;
                        case "NextDateDue":
                            Dg_Maintenance.Columns[i].HeaderText = @"Next Due Date";
                            break;
                        case "NextThruputQtyDue":
                            Dg_Maintenance.Columns[i].HeaderText = @"Next Thruput Quantity Due";
                            break;
                        case "MaintenanceState":
                            Dg_Maintenance.Columns[i].HeaderText = @"Maintenance State";
                            _indexMaintenanceState = Dg_Maintenance.Columns[i].Index;
                            break;
                    }

                }
            }
          
        }

        private void MoveWorkerDoWork(object sender, DoWorkEventArgs e)
        {
            var oContainerStatus =
                         Mes.GetContainerStatusDetails(_mesData, Tb_SerialNumber.Text,
                           _mesData.DataCollectionName);
            if (oContainerStatus != null)
            {
               _moveWorker.ReportProgress(1, @"Container Move In Attempt 1");
                var transaction =
                      Mes.ExecuteMoveIn(_mesData, oContainerStatus.ContainerName.Value, _dMoveIn);
                var resultMoveIn = transaction.Result || transaction.Message ==
                    "Move-in has already been performed for this operation.";
                if (!resultMoveIn && transaction.Message.Contains("TimeOut"))
                {
                    _moveWorker.ReportProgress(1, @"Container Move In Attempt 2");
                    transaction = Mes.ExecuteMoveIn(_mesData, oContainerStatus.ContainerName.Value,
                        _dMoveIn);
                    resultMoveIn = transaction.Result || transaction.Message ==
                        "Move-in has already been performed for this operation.";
                    if (!resultMoveIn && transaction.Message.Contains("TimeOut"))
                    {
                        _moveWorker.ReportProgress(1, @"Container Move In Attempt 3");
                        transaction = Mes.ExecuteMoveIn(_mesData, oContainerStatus.ContainerName.Value,
                            _dMoveIn);
                        resultMoveIn = transaction.Result || transaction.Message ==
                            "Move-in has already been performed for this operation.";
                    }
                }

                if (resultMoveIn)
                {
                    //Consume Component
                    _moveWorker.ReportProgress(2, @"Container Component Issue");
                    var listIssue = new List<dynamic>();
                    if (_backendComponent.MasterCarton.Enabled)
                        listIssue.Add(_backendComponent.MasterCarton.ToIssueActualDetail("Repair"));
                    if (_backendComponent.MasterCartonLabel.Enabled)
                        listIssue.Add(_backendComponent.MasterCartonLabel.ToIssueActualDetail("Repair"));
                    if (_backendComponent.ColorBox.Enabled)
                        listIssue.Add(_backendComponent.ColorBox.ToIssueActualDetail("Repair"));
                    if (_backendComponent.ColorBoxLabel.Enabled)
                        listIssue.Add(_backendComponent.ColorBoxLabel.ToIssueActualDetail("Repair"));

                    if (listIssue.Count > 0)
                    {
                        var transIssue = Mes.ExecuteComponentIssue(_mesData,
                            oContainerStatus.ContainerName.Value,
                            listIssue);
                        if (!transIssue.Result)
                        {
                            e.Result = (BackEndState.ComponentIssueFailed);
                            return;
                        }
                    }

                    _dMoveOut = DateTime.Now;
                    _moveWorker.ReportProgress(3, @"Container Move Standard Attempt 1");
                    var resultMoveStd = Mes.ExecuteMoveStandard(_mesData,
                        oContainerStatus.ContainerName.Value, _dMoveOut);
                    if (!resultMoveStd.Result)
                    {
                        _moveWorker.ReportProgress(3, @"Get Container Position 1");
                        var posAfterMoveStd = Mes.GetCurrentContainerStep(_mesData, oContainerStatus.ContainerName.Value);
                        resultMoveStd.Result |= !posAfterMoveStd.Contains("Backend");
                        if (!resultMoveStd.Result)
                        {
                            _dMoveOut = DateTime.Now;
                            _moveWorker.ReportProgress(3, @"Container Move Standard Attempt 2");
                            resultMoveStd = Mes.ExecuteMoveStandard(_mesData,
                                oContainerStatus.ContainerName.Value, _dMoveOut);

                            if (!resultMoveStd.Result)
                            {
                                _moveWorker.ReportProgress(3, @"Get Container Position 2");
                                posAfterMoveStd = Mes.GetCurrentContainerStep(_mesData, oContainerStatus.ContainerName.Value);
                                resultMoveStd.Result |= !posAfterMoveStd.Contains("Backend");
                                if (!resultMoveStd.Result)
                                {
                                    _dMoveOut = DateTime.Now;
                                    _moveWorker.ReportProgress(3, @"Container Move Standard Attempt 3");
                                    resultMoveStd = Mes.ExecuteMoveStandard(_mesData,
                                        oContainerStatus.ContainerName.Value, _dMoveOut);
                                    if (!resultMoveStd.Result)
                                    {
                                        _moveWorker.ReportProgress(3, @"Get Container Position 3");
                                        posAfterMoveStd = Mes.GetCurrentContainerStep(_mesData, oContainerStatus.ContainerName.Value);
                                        resultMoveStd.Result |= !posAfterMoveStd.Contains("Backend");
                                    }
                                }
                            }
                        }
                    }

                    ThreadHelper.ControlSetText( lbMoveOut, _dMoveOut.ToString(Mes.DateTimeStringFormat));

                    //Update Counter
                    if (resultMoveStd.Result)
                    {
                        var currentPos =
                              Mes.GetCurrentContainerStep(_mesData, oContainerStatus.ContainerName.Value);
                        Mes.UpdateOrCreateFinishGoodRecordToCached(_mesData,
                          oContainerStatus.MfgOrderName?.Value, oContainerStatus.ContainerName.Value,
                          currentPos);
                        _mesUnitCounter.UpdateCounter(oContainerStatus.ContainerName.Value);
                        MesUnitCounter.Save(_mesUnitCounter);
                        ThreadHelper.ControlSetText(Tb_BackEndQty, _mesUnitCounter.Counter.ToString());
                    }

                    e.Result=(resultMoveStd.Result
                      ? BackEndState.ScanUnitSerialNumber
                      : BackEndState.MoveInOkMoveFail);
                    return;
                }

                // check if fail by maintenance Past Due
                var transPastDue = Mes.GetMaintenancePastDue(_mesData.MaintenanceStatusDetails);
                if (transPastDue.Result)
                {
                    KryptonMessageBox.Show(this, "This resource under maintenance, need to complete!",
                        "Move In",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                e.Result=(BackEndState.MoveInFail);
                return;
            }

            e.Result = BackEndState.UnitNotFound;
        }

        private void MoveWorkerProgress(object sender, ProgressChangedEventArgs e)
        {
            var cmd = (string) e.UserState;
            lblCommand.Text = cmd;
        }

        private void MoveWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result == null) return;
            var state = (BackEndState) e.Result;
            SetBackendState(state);
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

        private void SetBackendState(BackEndState backEndState)
        {
            _backEndState = backEndState;
            switch (_backEndState)
            {
                case BackEndState.PlaceUnit:
                    _readScanner = false;
                    lblCommand.ForeColor = Color.Red;
                    lblCommand.Text = @"Resource is not in ""Up"" condition!";
                    break;
                case BackEndState.ScanUnitSerialNumber:
                    _readScanner = true;
                    ActiveControl = Tb_Scanner;
                    ClrContainer();
                    lblCommand.ForeColor = Color.LimeGreen;
                    lblCommand.Text = @"Scan Unit Serial Number!";
                    if (_mesData.ResourceStatusDetails == null || _mesData.ResourceStatusDetails?.Availability != "Up")
                    {
                        SetBackendState(BackEndState.PlaceUnit);
                        break;
                    }

                    // check if fail by maintenance Past Due
                    var transPastDue = Mes.GetMaintenancePastDue(_mesData.MaintenanceStatusDetails);
                    if (transPastDue.Result)
                    {
                        KryptonMessageBox.Show(this, "This resource under maintenance, need to complete!", "Move In",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;
                    }

                    break;
                case BackEndState.CheckUnitStatus:
                    lblCommand.Text = @"Checking Unit Status";
                    if (_mesData.ResourceStatusDetails == null || _mesData.ResourceStatusDetails?.Availability != "Up")
                    {
                          SetBackendState(BackEndState.PlaceUnit);
                        break;
                    }

                    // check if fail by maintenance Past Due
                    transPastDue = Mes.GetMaintenancePastDue(_mesData.MaintenanceStatusDetails);
                    if (transPastDue.Result)
                    {
                        KryptonMessageBox.Show(this, "This resource under maintenance, need to complete!", "Move In",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;
                    }

                    _readScanner = false;
                    var oContainerStatus =   Mes.GetContainerStatusDetails(_mesData, Tb_SerialNumber.Text,
                        _mesData.DataCollectionName);
                    if (oContainerStatus != null)
                    {
                        if (oContainerStatus.Qty == 0)
                        {
                            _wrongContainerPosition = "Scrap";
                              SetBackendState(BackEndState.WrongPosition);
                            break;
                        }
                        if (oContainerStatus.Operation?.Name != _mesData.OperationName)
                        {
                            _wrongContainerPosition = oContainerStatus.Operation?.Name;
                              SetBackendState(BackEndState.WrongPosition);
                            break;
                        }
                       
                        if (oContainerStatus.MfgOrderName?.ToString() != "" && (_mesData.ManufacturingOrder == null ||
                                oContainerStatus.MfgOrderName?.Value != _mesData.ManufacturingOrder?.Name?.Value))
                        {
                            lblLoadingPo.Visible = true;
                            ClearPo();
                            var mfg =   Mes.GetMfgOrder(_mesData, oContainerStatus.MfgOrderName?.ToString());
                            if (mfg == null)
                            {
                                lblLoadingPo.Visible = false;
                                KryptonMessageBox.Show(this, "Failed To Get Manufacturing Order Information",
                                    "Check Unit",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                                  SetBackendState(BackEndState.ScanUnitSerialNumber);
                                break;
                            }


                            _mesData.SetManufacturingOrder(mfg);
                            if (mfg.MaterialList!=null)
                            {_backendComponent = new BackendComponent
                            {
                                MasterCarton =
                                {
                                    Enabled = mfg.MaterialList.Where(x =>
                                        x.Product.Name.IndexOf("801", StringComparison.Ordinal) == 0 ||
                                        x.Product.Name.IndexOf("820", StringComparison.Ordinal) == 0 &&
                                        x.wikScanning?.Value == "X" && x.wikScanning != null).ToList().Count > 0

                                },
                                ColorBox =
                                {
                                    Enabled = mfg.MaterialList.Where(x =>
                                        x.Product.Name.IndexOf("802", StringComparison.Ordinal) == 0 &&
                                        x.wikScanning?.Value == "X" && x.wikScanning != null).ToList().Count > 0
                                },
                                ColorBoxLabel =
                                {
                                    Enabled = mfg.MaterialList.Where(x =>
                                        x.Product.Name.IndexOf("809", StringComparison.Ordinal) == 0 &&
                                        x.wikScanning?.Value == "X" && x.wikScanning != null).ToList().Count > 0
                                },
                                MasterCartonLabel =
                                {
                                    Enabled = mfg.MaterialList.Where(x =>
                                        x.Product.Name.IndexOf("809", StringComparison.Ordinal) == 0 &&
                                        x.wikScanning?.Value == "X" && x.wikScanning != null).ToList().Count > 1
                                },
                            };}
                            else
                            {
                                _backendComponent = new BackendComponent();
                            }

                            _backendComponent.MasterCarton.Enabled &=
                                _componentSetting.MasterCartonEnable == EnableDisable.Enable;
                            _backendComponent.ColorBox.Enabled &=
                                _componentSetting.ColorBoxEnable == EnableDisable.Enable;
                            _backendComponent.MasterCartonLabel.Enabled &=
                                _componentSetting.LabelEnable == EnableDisable.Enable;
                            _backendComponent.ColorBoxLabel.Enabled &=
                                _componentSetting.LabelEnable == EnableDisable.Enable;

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
                            Tb_ProductDesc.Text = oContainerStatus.ProductDescription?.Value;

                            var img =   Mes.GetImage(_mesData, oContainerStatus.Product.Name);
                            pictureBox1.ImageLocation = img.Identifier.Value;

                            if (_mesUnitCounter != null)
                            {
                                  _mesUnitCounter.StopPoll();
                                  _mesUnitCounter.Synchronize();
                            }

                            _mesUnitCounter = MesUnitCounter.Load(MesUnitCounter.GetFileName(mfg.Name.Value));


                            _mesUnitCounter.SetActiveMfgOrder(mfg.Name.Value);

                            _mesUnitCounter.InitPoll(_mesData);
                            _mesUnitCounter.StartPoll();
                            MesUnitCounter.Save(_mesUnitCounter);

                            Tb_BackEndQty.Text = _mesUnitCounter.Counter.ToString();
                            lblLoadingPo.Visible = false;
                        }

                        _dMoveIn = DateTime.Now;
                        lbMoveIn.Text = _dMoveIn.ToString(Mes.DateTimeStringFormat);
                        lbMoveOut.Text = "";
                        _backendComponent.ResetValue();
                        if (_backendComponent.Completed)
                        {
                              SetBackendState(BackEndState.UpdateMoveInMove);
                            break;
                        }

                        SetBackendState(BackEndState.ScanAny);
                        break;
                    }

                    var containerStep =
                          Mes.GetCurrentContainerStep(_mesData, Tb_SerialNumber.Text); // try get operation pos
                    if (containerStep != null && !_mesData.OperationName.Contains(containerStep))
                    {
                        _wrongContainerPosition = containerStep;
                          SetBackendState(BackEndState.WrongPosition);
                        break;
                    }

                      SetBackendState(BackEndState.UnitNotFound);
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
                    lblCommand.Text = @"Container Move In";
                    if (_moveWorker.IsBusy)
                    {
                        _moveWorker.Abort();
                    }
                    _moveWorker.RunWorkerAsync(Tb_SerialNumber.Text);

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
                    lblCommand.Text = $@"Wrong Operation, Container in {_wrongContainerPosition}";
                    break;
                case BackEndState.WrongComponent:
                    _readScanner = true;
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
                    _readScanner = true;
                    lblCommand.Text = @"Cannot Find Component in Bill of Material";
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

        private   void GetStatusMaintenanceDetails()
        {
            try
            {
                var maintenanceStatusDetails =   Mes.GetMaintenanceStatusDetails(_mesData);
                _mesData.SetMaintenanceStatusDetails(maintenanceStatusDetails);
                if (maintenanceStatusDetails != null)
                {
                    getMaintenanceStatusDetailsBindingSource.DataSource =
                        new BindingList<GetMaintenanceStatusDetails>(maintenanceStatusDetails);
                    Dg_Maintenance.DataSource = getMaintenanceStatusDetailsBindingSource;
                    //get past due, warning, and tolerance
                    var pastDue = maintenanceStatusDetails.Where(x => x.MaintenanceState == "Past Due").ToList();
                    var due = maintenanceStatusDetails.Where(x => x.MaintenanceState == "Due").ToList();
                    var pending = maintenanceStatusDetails.Where(x => x.MaintenanceState == "Pending").ToList();

                    if (pastDue.Count > 0)
                    {
                        lblResMaintMesg.Text = @"Resource Maintenance Past Due";
                        lblResMaintMesg.BackColor = Color.Red;
                        lblResMaintMesg.Visible = true;
                        if (_mesData?.ResourceStatusDetails?.Reason?.Name != "Planned Maintenance")
                        {
                              Mes.SetResourceStatus(_mesData, "BE - Planned Downtime", "Planned Maintenance");
                        }

                        return;
                    }

                    if (due.Count > 0)
                    {
                        lblResMaintMesg.Text = @"Resource Maintenance Due";
                        lblResMaintMesg.BackColor = Color.Orange;
                        lblResMaintMesg.Visible = true;
                        return;
                    }

                    if (pending.Count > 0)
                    {
                        lblResMaintMesg.Text = @"Resource Maintenance Pending";
                        lblResMaintMesg.BackColor = Color.Yellow;
                        lblResMaintMesg.Visible = true;
                        return;
                    }
                }

                lblResMaintMesg.Visible = false;
                lblResMaintMesg.Text = "";
                getMaintenanceStatusDetailsBindingSource.DataSource = null;
            }
            catch (Exception ex)
            {
                ex.Source = AppSettings.AssemblyName == ex.Source
                    ? MethodBase.GetCurrentMethod()?.Name
                    : MethodBase.GetCurrentMethod()?.Name + "." + ex.Source;
                EventLogUtil.LogErrorEvent(ex.Source, ex);
            }
        }

        private void GetStatusOfResource()
        {
            try
            {
                var resourceStatus =   Mes.GetResourceStatusDetails(_mesData);
                if (resourceStatus != null)
                {
                    _mesData.SetResourceStatusDetails(resourceStatus);
                    if (resourceStatus.Status != null) Tb_StatusCode.Text = resourceStatus.Reason?.Name;
                    if (resourceStatus.Availability != null)
                    {
                        if (resourceStatus.Availability.Value == "Up")
                        {
                            Tb_StatusCode.StateCommon.Content.Color1 =
                                resourceStatus.Reason?.Name == "Quality Inspection" ? Color.Orange : Color.Green;
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
                ex.Source = AppSettings.AssemblyName == ex.Source
                    ? MethodBase.GetCurrentMethod()?.Name
                    : MethodBase.GetCurrentMethod()?.Name + "." + ex.Source;
                EventLogUtil.LogErrorEvent(ex.Source, ex);
            }
        }

        private   void GetStatusOfResourceDetail()
        {
            try
            {
                var resourceStatus =   Mes.GetResourceStatusDetails(_mesData);
                if (resourceStatus != null)
                {
                    _mesData.SetResourceStatusDetails(resourceStatus);
                    if (resourceStatus.Status != null) Cb_StatusCode.Text = resourceStatus.Status.Name;
                      Task.Delay(1000);
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

        private   void GetResourceStatusCodeList()
        {
            try
            {
                var oStatusCodeList =   Mes.GetListResourceStatusCode(_mesData);
                if (oStatusCodeList != null)
                {
                    Cb_StatusCode.DataSource = oStatusCodeList
                        .Where(x => x.Name.IndexOf("BE", StringComparison.Ordinal) == 0).ToList();
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

        #endregion

        #region COMPONENT EVENT

        private   void TimerRealtime_Tick(object sender, EventArgs e)
        {
              GetStatusOfResource();
              GetStatusMaintenanceDetails();
        }

        private   void btnResetState_Click(object sender, EventArgs e)
        {
            if (_backEndState == BackEndState.WaitPreparation) return;
              SetBackendState(BackEndState.ScanUnitSerialNumber);
            Tb_Scanner.Focus();
        }

        private bool _readScanner;
        private bool _ignoreScanner;
        private BackendComponent _backendComponent;
        private DateTime _dMoveOut;
        private readonly int _indexMaintenanceState;
        private string _wrongContainerPosition;
        private MesUnitCounter _mesUnitCounter;
        private bool _allowClose;
        private bool _sortAscending;
        private BindingList<FinishedGood> _bindingList;
        private BackgroundWorker _syncWorker;
        private readonly AbortableBackgroundWorker _moveWorker;

        private   void Tb_Scanner_KeyUp(object sender, KeyEventArgs e)
        {
            if (!_readScanner) Tb_Scanner.Clear();
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
                            if (Tb_SerialNumber.Text.Length != 19)
                            {
                                _ignoreScanner = false;
                                break;
                            }

                            SetBackendState(BackEndState.CheckUnitStatus);
                            break;
                        case BackEndState.ComponentNotFound:
                        case BackEndState.WrongComponent:
                        case BackEndState.ScanAny:
                            if (_backendComponent.Completed)
                            {
                                  SetBackendState(BackEndState.UpdateMoveInMove);
                                break;
                            }

                            var scanned = Tb_Scanner.Text.Trim();
                            //validate
                            if (scanned.Length >= 10)
                            {
                                var valid = _mesData.ManufacturingOrder?.MaterialList?.Where(x =>
                                        x.Product.Name.IndexOf(scanned.Substring(0, 10), StringComparison.Ordinal) == 0)
                                    .ToList();
                                if (valid != null && valid.Count > 0)
                                {
                                    var distinct = scanned.Substring(0, 3);
                                    //distinguish component

                                    if (distinct == "801" || distinct == "820")
                                    {
                                        if (_backendComponent.MasterCarton.Enabled)
                                        {
                                            if (valid[0].wikScanning != "X" || valid[0].wikScanning == null)
                                            {
                                                  SetBackendState(BackEndState.WrongComponent);
                                                break;
                                            }
                                        }
                                        else
                                        {
                                              SetBackendState(BackEndState.WrongComponent);
                                            break;
                                        }

                                        _backendComponent.MasterCarton.Value = scanned;
                                        _backendComponent.MasterCarton.QuantityRequired =
                                            valid[0].QtyRequired.Value;
                                        Tb_MasterCarton.Text = scanned;
                                    }

                                    if (distinct == "802")
                                    {
                                        if (_backendComponent.ColorBox.Enabled)
                                        {

                                            if (valid[0].wikScanning != "X" || valid[0].wikScanning == null)
                                            {
                                                  SetBackendState(BackEndState.WrongComponent);
                                                break;
                                            }
                                        }
                                        else
                                        {
                                              SetBackendState(BackEndState.WrongComponent);
                                            break;
                                        }

                                        _backendComponent.ColorBox.Value = scanned;
                                        _backendComponent.ColorBox.QuantityRequired = valid[0].QtyRequired.Value;
                                        Tb_ColorBox.Text = scanned;
                                    }

                                    if (distinct == "809" && _backendComponent.ColorBoxLabel.Value == null ||
                                        _backendComponent.ColorBoxLabel.Value == scanned)
                                    {
                                        if (_backendComponent.ColorBoxLabel.Enabled)
                                        {

                                            if (valid[0].wikScanning != "X" || valid[0].wikScanning == null)
                                            {
                                                  SetBackendState(BackEndState.WrongComponent);
                                                break;
                                            }
                                        }
                                        else
                                        {
                                              SetBackendState(BackEndState.WrongComponent);
                                            break;
                                        }

                                        _backendComponent.ColorBoxLabel.Value = scanned;
                                        _backendComponent.ColorBoxLabel.QuantityRequired = valid[0].QtyRequired.Value;
                                        Tb_ColorBoxLabel.Text = scanned;
                                    }
                                    else
                                    {

                                        if (distinct == "809" && _backendComponent.MasterCartonLabel.Value == null
                                                              && !_backendComponent.ColorBoxLabel.Value
                                                                  .Equals(scanned) ||
                                            _backendComponent.MasterCartonLabel.Value == scanned)
                                        {
                                            if (_backendComponent.MasterCartonLabel.Enabled)
                                            {

                                                if (valid[0].wikScanning != "X" || valid[0].wikScanning == null)
                                                {
                                                      SetBackendState(BackEndState.WrongComponent);
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                  SetBackendState(BackEndState.WrongComponent);
                                                break;
                                            }

                                            _backendComponent.MasterCartonLabel.Value = scanned;
                                            _backendComponent.MasterCartonLabel.QuantityRequired =
                                                valid[0].QtyRequired.Value;
                                            Tb_MasterCartonLabel.Text = scanned;
                                        }
                                    }

                                    if (distinct != "809" && distinct != "802" &&
                                        !(distinct == "801" || distinct == "820"))
                                    {
                                          SetBackendState(BackEndState.WrongComponent);
                                        break;
                                    }
                                }
                                else
                                {
                                      SetBackendState(BackEndState.ComponentNotFound);
                                    break;
                                }
                            }

                            if (_backendComponent.Completed)
                            {
                                  SetBackendState(BackEndState.UpdateMoveInMove);
                            }

                            break;
                    }
                }

                _ignoreScanner = false;
                Tb_Scanner.Clear();
            }
        }

        #endregion

        private   void Main_Load(object sender, EventArgs e)
        {
              GetStatusOfResource();
              GetStatusMaintenanceDetails();
              GetResourceStatusCodeList();
              SetBackendState(BackEndState.WaitPreparation);
        }

        private void ClearPo()
        {
            Tb_PO.Clear();
            Tb_Product.Clear();
            Tb_ProductDesc.Clear();
            Tb_BackEndQty.Clear();
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

        }

        private void kryptonGroupBox2_Panel_Paint(object sender, PaintEventArgs e)
        {

        }

        private   void Cb_StatusCode_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                var oStatusCode =   Mes.GetResourceStatusCode(_mesData,
                    Cb_StatusCode.SelectedValue != null ? Cb_StatusCode.SelectedValue.ToString() : "");
                if (oStatusCode != null)
                {
                    Tb_StatusCodeM.Text = oStatusCode.Availability.ToString();
                    if (oStatusCode.ResourceStatusReasons != null)
                    {
                        var oStatusReason =
                              Mes.GetResourceStatusReasonGroup(_mesData, oStatusCode.ResourceStatusReasons.Name);
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
                ex.Source = AppSettings.AssemblyName == ex.Source
                    ? MethodBase.GetCurrentMethod()?.Name
                    : MethodBase.GetCurrentMethod()?.Name + "." + ex.Source;
                EventLogUtil.LogErrorEvent(ex.Source, ex);
            }
        }

        private   void btnSetMachineStatus_Click(object sender, EventArgs e)
        {
            try
            {
                var result = false;
                if (Cb_StatusCode.Text != "" && Cb_StatusReason.Text != "")
                {
                    result =   Mes.SetResourceStatus(_mesData, Cb_StatusCode.Text, Cb_StatusReason.Text);
                }
                else if (Cb_StatusCode.Text != "")
                {
                    result =   Mes.SetResourceStatus(_mesData, Cb_StatusCode.Text, "");
                }

                  GetStatusOfResourceDetail();
                  GetStatusOfResource();
                KryptonMessageBox.Show(result ? "Setup status successful" : "Setup status failed");

            }
            catch (Exception ex)
            {
                ex.Source = AppSettings.AssemblyName == ex.Source
                    ? MethodBase.GetCurrentMethod()?.Name
                    : MethodBase.GetCurrentMethod()?.Name + "." + ex.Source;
                EventLogUtil.LogErrorEvent(ex.Source, ex);
            }
        }

        private async  void kryptonNavigator1_SelectedPageChanged(object sender, EventArgs e)
        {
            if (kryptonNavigator1.SelectedIndex == 0)
            {
                ActiveControl = Tb_Scanner;
            }

            if (kryptonNavigator1.SelectedIndex == 1)
            {
                  GetStatusOfResourceDetail();
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
               if(!_syncWorker.IsBusy) lblLoading.Visible = false;
            }

        }

        private   async Task GetFinishedGoodRecord()
        {
            if (_mesData == null) return;

            var data = await  Mes.GetFinishGoodRecordFromCached(_mesData, _mesData.ManufacturingOrder?.Name.ToString());
            if (data != null)
            {
                var list =   Mes.FinishGoodToFinishedGood(data);
                _bindingList = new BindingList<FinishedGood>(list);
                finishedGoodBindingSource.DataSource = _bindingList;
                kryptonDataGridView1.DataSource = finishedGoodBindingSource;
                Tb_FinishedGoodCounter.Text = list.Length.ToString();
            }
        }

        private void kryptonNavigator1_Selecting(object sender,
            ComponentFactory.Krypton.Navigator.KryptonPageCancelEventArgs e)
        {
            if (e.Index != 1 && e.Index != 2) return;

            using (var ss = new LoginForm24(e.Index == 1 ? "Maintenance" : "Quality"))
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

        private   void btnCallMaintenance_Click(object sender, EventArgs e)
        {
            try
            {
                var dlg = MessageBox.Show(@"Are you sure want to call maintenance?", @"Call Maintenance",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dlg == DialogResult.No)
                {
                    return;
                }

                var result =   Mes.SetResourceStatus(_mesData, "BE - Internal Downtime", "Maintenance");
                  GetStatusOfResource();
                KryptonMessageBox.Show(result ? "Setup status successful" : "Setup status failed");
            }
            catch (Exception ex)
            {
                ex.Source = AppSettings.AssemblyName == ex.Source
                    ? MethodBase.GetCurrentMethod()?.Name
                    : MethodBase.GetCurrentMethod()?.Name + "." + ex.Source;
                EventLogUtil.LogErrorEvent(ex.Source, ex);
            }
        }

        private   void btnFinishPreparation_Click(object sender, EventArgs e)
        {
            if (_mesData.ResourceStatusDetails?.Reason.Name == "Maintenance") return;
            var result =   Mes.SetResourceStatus(_mesData, "BE - Productive Time", "Pass");
              GetStatusOfResource();
            if (result)
            {
                btnFinishPreparation.Enabled = false;
                btnStartPreparation.Enabled = true;
                  SetBackendState(BackEndState.ScanUnitSerialNumber);
            }
        }

        private   void btnStartPreparation_Click(object sender, EventArgs e)
        {
            ClearPo();
            if (_mesData.ResourceStatusDetails?.Reason?.Name == "Maintenance") return;
            if (_mesData.ResourceStatusDetails?.Reason?.Name == "Planned Maintenance") return;

            _mesData.SetManufacturingOrder(null);
            var result =   Mes.SetResourceStatus(_mesData, "BE - Planned Downtime", "Preparation");
              GetStatusOfResource();
            if (result)
            {
                  SetBackendState(BackEndState.WaitPreparation);
                btnFinishPreparation.Enabled = true;
                btnStartPreparation.Enabled = false;
            }
        }


        private void Tb_Scanner_TextChanged(object sender, EventArgs e)
        {

        }

        private void Dg_Maintenance_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            try
            {
                foreach (DataGridViewRow row in Dg_Maintenance.Rows)
                {
                    switch (Convert.ToString(row.Cells[_indexMaintenanceState].Value))
                    {
                        //Console.WriteLine(Convert.ToString(row.Cells["MaintenanceState"].Value));
                        case "Pending":
                            row.DefaultCellStyle.BackColor = Color.Yellow;
                            break;
                        case "Due":
                            row.DefaultCellStyle.BackColor = Color.Orange;
                            break;
                        case "Past Due":
                            row.DefaultCellStyle.BackColor = Color.Red;
                            break;
                    }
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

        private void Btn_SetConfig_Click(object sender, EventArgs e)
        {
            try
            {
                _tempComponentSetting.SaveFile();
                _componentSetting = BackendComponentSetting.Load(BackendComponentSetting.FileName);
                KryptonMessageBox.Show("Saved Config File Successfully!");
            }
            catch
            {
                KryptonMessageBox.Show("Fail to save Config File!");
            }
        }

        private   void  Closing()
        {
            if (_mesUnitCounter != null)
            {
                  _mesUnitCounter.StopPoll();
            }

            _allowClose = true;
            Close();
        }

        private   void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_allowClose)
            {
                var dlg = MessageBox.Show(@"Are you sure want to close Application?", @"Close Application",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (dlg == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            if (_allowClose)
            {
                e.Cancel = true;
                Environment.Exit(Environment.ExitCode);
            }

            e.Cancel = true;
               Closing();
        }

        private void btnCallMaintenance_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        private void SyncWorkerProgress(object sender, ProgressChangedEventArgs e)
        {

        }

        private void SyncWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var data = (List<IFinishGoodRecord>)e.Result;
            var list = Mes.FinishGoodToFinishedGood(data);
            _bindingList = new BindingList<FinishedGood>(list);
            finishedGoodBindingSource.DataSource = _bindingList;
            kryptonDataGridView1.DataSource = finishedGoodBindingSource;
            Tb_FinishedGoodCounter.Text = list.Length.ToString();
            lblLoading.Visible = false;
        }
        private void SyncDoWork(object sender, DoWorkEventArgs e)
        {
            var temp = Mes.GetFinishGoodRecordSyncWithServer(_mesData, _mesData.ManufacturingOrder?.Name.ToString()).Result;
            var data = temp == null ? new List<IFinishGoodRecord>() : temp.ToList();
            e.Result = data;
        }
        private void btnSynchronize_Click(object sender, EventArgs e)
        {
            if (_syncWorker.IsBusy) return;
            if (_mesData == null) return;
            if (_mesData.ManufacturingOrder == null) return;
            lblLoading.Visible = true;
            _syncWorker.RunWorkerAsync();
        }


        private void kryptonDataGridView1_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (_bindingList == null) return;
            kryptonDataGridView1.DataSource = _sortAscending ? _bindingList.OrderBy(kryptonDataGridView1.Columns[e.ColumnIndex].DataPropertyName).ToList() : _bindingList.OrderBy(kryptonDataGridView1.Columns[e.ColumnIndex].DataPropertyName).Reverse().ToList();
            _sortAscending = !_sortAscending;
        }
    }
}
