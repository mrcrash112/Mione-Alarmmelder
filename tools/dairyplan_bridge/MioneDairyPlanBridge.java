import RDM_DP.Com.CORBA.DPPC_Command;
import RDM_DP.Com.CORBA.PSU_Command;
import RDM_DP.Com.CORBA.enum_RobotPosition;
import RDM_DP.Com.CORBA.DPPC_CommandPackage.enum_SamplingBox;
import RDM_DP.Com.CORBA.DPPC_CommandPackage.struct_DPB_SamplingCmd;
import RDM_DP.Com.CORBA.PSU_CommandPackage.enum_Box;
import RDM_DP.Com.CORBA.PSU_CommandPackage.struct_DPB_AugerCalibrationCmd;
import RDM_DP.Com.CORBA.PSU_CommandPackage.struct_DPB_BoxCmd;
import RDM_DP.Com.CORBA.PSU_CommandPackage.struct_DPB_RobotPosition;
import RDM_DP.Com.CORBA.RmsCORBACompManPackage.Device;
import RDM_DP.Com.CORBA.RmsCORBACompManPackage.DeviceType;
import RDM_DP.Com.Utils.RdmCompMan;
import gea.westfaliasurge.ams.rdm.logic.BCUListener;
import gea.westfaliasurge.ams.rdm.logic.DPPCListener;
import gea.westfaliasurge.ams.rdm.logic.PSUListener;

public final class MioneDairyPlanBridge {
    private MioneDairyPlanBridge() { }

    public static void main(String[] args) throws Exception {
        String ior = value(args, "--ior");
        String command = value(args, "--command");
        if (ior.length() == 0) throw new IllegalArgumentException("--ior fehlt");
        if (command.length() == 0) throw new IllegalArgumentException("--command fehlt");

        RdmCompMan manager = new RdmCompMan();
        try {
            manager.init(ior);
            initializeListeners(manager);
            execute(manager, command, args);
            refreshLiveData(manager, command);
            System.out.println("OK " + command);
        } finally {
            try { manager.shutDown(); } catch (Throwable ignored) { }
        }
    }

    private static void execute(RdmCompMan manager, String command, String[] args) throws Exception {
        if ("initializeRobot".equals(command)) { psu(manager).initalizeRobot(); return; }
        if ("initializeSystem".equals(command)) { psu(manager).initializeSystem(); return; }
        if ("enableBox".equals(command)) { psu(manager).enableBox(boxCmd(args)); return; }
        if ("disableBox".equals(command)) { psu(manager).disableBox(boxCmd(args)); return; }
        if ("startAutomaticOperation".equals(command)) { psu(manager).startAutomaticOperation(); return; }
        if ("stopAutomaticOperation".equals(command)) { psu(manager).stopAutomaticOperation(); return; }
        if ("startSystemCleaning".equals(command)) { psu(manager).startSystemCleaning(); return; }
        if ("stopSystemCleaning".equals(command)) { psu(manager).stopSystemCleaning(); return; }
        if ("startShortCleaning".equals(command)) { psu(manager).startShortCleaning(boxCmd(args)); return; }
        if ("stopShortCleaning".equals(command)) { psu(manager).stopShortCleaning(boxCmd(args)); return; }
        if ("stopMilking".equals(command)) { psu(manager).stopMilking(boxCmd(args)); return; }
        if ("setManualMilkingOneBox".equals(command)) { psu(manager).setManualMilkingOneBox(boxCmd(args)); return; }
        if ("setAutomaticMilkingOneBox".equals(command)) { psu(manager).setAutomaticMilkingOneBox(boxCmd(args)); return; }
        if ("moveRobotToPosition".equals(command)) { psu(manager).moveRobotToPosition(robotPositionCmd(args)); return; }
        if ("startAugerCalibration".equals(command)) { psu(manager).startAugerCalibration(augerCalibrationCmd(args)); return; }
        if ("stopAugerCalibration".equals(command)) { psu(manager).stopAugerCalibration(boxCmd(args)); return; }
        if ("startPreparationWaterTanks".equals(command)) { psu(manager).startPreparationWaterTanks(); return; }
        if ("resetAlarm".equals(command)) { psu(manager).resetAlarm(); return; }
        if ("startSamplingSession".equals(command)) { dppc(manager).startSamplingSession(); return; }
        if ("stopSamplingSession".equals(command)) { dppc(manager).stopSamplingSession(); return; }
        if ("pauseSampling".equals(command)) { dppc(manager).pauseSampling(samplingCmd(args)); return; }
        if ("resumeSampling".equals(command)) { dppc(manager).resumeSampling(samplingCmd(args)); return; }
        throw new IllegalArgumentException("Unbekannte Funktion: " + command);
    }

    private static void refreshLiveData(RdmCompMan manager, String command) {
        try {
            if (isPsuCommand(command)) {
                psu(manager).sendParameters();
            }
            if (isDppcCommand(command)) {
                dppc(manager).updateAreaCounterXMLFile();
            }
        } catch (Throwable ignored) {
        }
    }

    private static boolean isPsuCommand(String command) {
        return "initializeRobot".equals(command) ||
            "initializeSystem".equals(command) ||
            "enableBox".equals(command) ||
            "disableBox".equals(command) ||
            "startAutomaticOperation".equals(command) ||
            "stopAutomaticOperation".equals(command) ||
            "startSystemCleaning".equals(command) ||
            "stopSystemCleaning".equals(command) ||
            "startShortCleaning".equals(command) ||
            "stopShortCleaning".equals(command) ||
            "stopMilking".equals(command) ||
            "setManualMilkingOneBox".equals(command) ||
            "setAutomaticMilkingOneBox".equals(command) ||
            "moveRobotToPosition".equals(command) ||
            "startAugerCalibration".equals(command) ||
            "stopAugerCalibration".equals(command) ||
            "startPreparationWaterTanks".equals(command) ||
            "resetAlarm".equals(command);
    }

    private static boolean isDppcCommand(String command) {
        return "startSamplingSession".equals(command) ||
            "stopSamplingSession".equals(command) ||
            "pauseSampling".equals(command) ||
            "resumeSampling".equals(command);
    }

    private static PSU_Command psu(RdmCompMan manager) throws Exception { return manager.getPSU(1); }
    private static DPPC_Command dppc(RdmCompMan manager) throws Exception { return manager.getDPPC(1); }

    private static void initializeListeners(RdmCompMan manager) throws Exception {
        Device[] devices = manager.componentList();
        for (int i = 0; i < devices.length; i++) {
            if (devices[i] == null) continue;
            if (devices[i].type == DeviceType.BCU) {
                BCUListener listener = new BCUListener(devices[i].iIndex);
                manager.setBCUListener(listener.boxNumber, listener);
            }
            else if (devices[i].type == DeviceType.PSU) {
                manager.setPSUListener(1, new PSUListener());
            }
            else if (devices[i].type == DeviceType.DPPC) {
                manager.setDPPCListener(1, new DPPCListener());
            }
        }
    }

    private static struct_DPB_BoxCmd boxCmd(String[] args) {
        return new struct_DPB_BoxCmd(box(number(value(args, "--box"), "boxNumber")));
    }

    private static struct_DPB_AugerCalibrationCmd augerCalibrationCmd(String[] args) {
        String raw = value(args, "--feeding-type");
        char feedingType = raw.length() == 0 ? '0' : raw.charAt(0);
        return new struct_DPB_AugerCalibrationCmd(box(number(value(args, "--box"), "boxNumber")), feedingType);
    }

    private static struct_DPB_RobotPosition robotPositionCmd(String[] args) {
        int value = number(value(args, "--robot-position"), "robotPosition");
        return new struct_DPB_RobotPosition(enum_RobotPosition.from_int(toZeroBased(value, 1, 9, "robotPosition")));
    }

    private static struct_DPB_SamplingCmd samplingCmd(String[] args) {
        int value = number(value(args, "--sampling-box"), "samplingBox");
        return new struct_DPB_SamplingCmd(enum_SamplingBox.from_int(toZeroBased(value, 1, 5, "samplingBox")));
    }

    private static enum_Box box(int value) {
        if (value == 0) return enum_Box.DPB_AllBoxes;
        return enum_Box.from_int(toZeroBased(value, 1, 5, "boxNumber"));
    }

    private static int toZeroBased(int value, int min, int max, String name) {
        if (value >= min && value <= max) return value - 1;
        if (value >= min - 1 && value <= max - 1) return value;
        throw new IllegalArgumentException(name + " ausserhalb des Bereichs " + min + "-" + max + ": " + value);
    }

    private static int number(String value, String name) {
        if (value.length() == 0) throw new IllegalArgumentException(name + " fehlt");
        return Integer.parseInt(value);
    }

    private static String value(String[] args, String name) {
        for (int i = 0; i + 1 < args.length; i++) {
            if (name.equals(args[i])) return args[i + 1];
        }
        return "";
    }
}
