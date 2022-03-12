using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackendGUI.Enumeration
{
    public enum BackEndState
    {
        PlaceUnit,
        ScanUnitSerialNumber,
        CheckUnitStatus,
        UnitNotFound,
        ScanProductSerialNumber,
        ScanColorBoxSerialNumber,
        ScanCartonBoxSerialNumber,
        ScanLabelSerialNumber,
        UpdateMoveInMove,
        MoveSuccess,
        MoveInOkMoveFail,
        MoveInFail,
        Done,
        WrongPosition
    }
}
