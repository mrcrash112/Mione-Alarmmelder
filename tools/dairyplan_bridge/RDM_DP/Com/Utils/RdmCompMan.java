package RDM_DP.Com.Utils;

import java.io.BufferedReader;
import java.io.FileReader;

import org.omg.CORBA.ORB;

import RDM_DP.Com.CORBA.DPPC_Command;
import RDM_DP.Com.CORBA.PSU_Command;
import RDM_DP.Com.CORBA.RmsCORBACompMan;
import RDM_DP.Com.CORBA.RmsCORBACompManHelper;
import RDM_DP.Com.CORBA.RmsCORBACompManPackage.Device;
import RDM_DP.Com.CORBA.RmsCORBACompManPackage.OutOfRange;
import RDM_DP.Com.CORBA.RmsCORBACompManPackage.DeviceType;
import RDM_DP.Com.Server.BCU_NotifyServant;
import RDM_DP.Com.Server.DPPC_NotifyServant;
import RDM_DP.Com.Server.PSU_NotifyServant;
import RDM_DP.Com.Utils.NotifyBCUListener;
import RDM_DP.Com.Utils.NotifyDPPCListener;
import RDM_DP.Com.Utils.NotifyPSUListener;
import gea.westfaliasurge.ams.rdm.logic.BCUListener;
import gea.westfaliasurge.ams.rdm.logic.DPPCListener;
import gea.westfaliasurge.ams.rdm.logic.PSUListener;
import de.pisoftware.CORBA.Server;

public class RdmCompMan {
    protected Server m_server;
    protected ORB orb;
    protected RmsCORBACompMan m_rccm;

    public RdmCompMan() {
    }

    public void init(String iorPath) {
        try {
            orb = ORB.init((String[]) null, System.getProperties());
            BufferedReader reader = new BufferedReader(new FileReader(iorPath));
            try {
                String ior = reader.readLine();
                org.omg.CORBA.Object obj = orb.string_to_object(ior);
                m_server = new Server();
                m_server.run();
                m_rccm = RmsCORBACompManHelper.narrow(obj);
                if (m_rccm == null) {
                    throw new IllegalStateException("Konnte RmsCORBACompMan nicht verbinden.");
                }
            } finally {
                try { reader.close(); } catch (Exception ignored) { }
            }
        } catch (Exception ex) {
            throw new RuntimeException("Error initializing RdmCompMan proxy", ex);
        }
    }

    public void reset() {
        // Intentionally no-op. We do not want to reset the live RDM session.
    }

    public void setPSUListener(int index, NotifyPSUListener listener) throws Exception {
        PSU_NotifyServant servant = new PSU_NotifyServant();
        org.omg.CORBA.Object activated = m_server.activateObject(servant);
        servant.setListener(listener);
        m_rccm.setPSUNotify(index, RDM_DP.Com.CORBA.PSU_NotifyHelper.narrow(activated));
    }

    public void setDPPCListener(int index, NotifyDPPCListener listener) throws Exception {
        DPPC_NotifyServant servant = new DPPC_NotifyServant();
        org.omg.CORBA.Object activated = m_server.activateObject(servant);
        servant.setListener(listener);
        m_rccm.setDPPCNotify(index, RDM_DP.Com.CORBA.DPPC_NotifyHelper.narrow(activated));
    }

    public void setBCUListener(int index, NotifyBCUListener listener) throws Exception {
        BCU_NotifyServant servant = new BCU_NotifyServant();
        org.omg.CORBA.Object activated = m_server.activateObject(servant);
        servant.setListener(listener);
        m_rccm.setBCUNotify(index, RDM_DP.Com.CORBA.BCU_NotifyHelper.narrow(activated));
    }

    public PSU_Command getPSU(int index) throws OutOfRange {
        return m_rccm.getPSU(index);
    }

    public DPPC_Command getDPPC(int index) throws OutOfRange {
        return m_rccm.getDPPC(index);
    }

    public Device[] componentList() {
        return m_rccm.componentList();
    }

    public void shutDown() {
        try {
            if (m_server != null) m_server.shutDown();
        } catch (Exception ignored) {
        }
    }
}
