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

public class RdmCompMan {
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
        // Intentionally no-op. The caller process is short-lived already.
    }
}
