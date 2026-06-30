import React, { useState, useEffect, useCallback } from 'react';
import { useToast } from '../../components/ui/Toast';
import Drawer from '../../components/ui/Drawer';
import api from '../../config/api';
import { MoreVertical, Edit, Trash2, Plus, Check, ShieldAlert, KeyRound } from 'lucide-react';
import { authAPI } from '../../api/auth.api';


export default function AdminsTab() {
  const [admins, setAdmins] = useState([]);
  const [loading, setLoading] = useState(false);
  const [adminDrawer, setAdminDrawer] = useState({ isOpen: false, data: null });
  const [credentialsDrawer, setCredentialsDrawer] = useState({ isOpen: false, data: null });
  const [activeDropdown, setActiveDropdown] = useState(null);
  const [dropdownPos, setDropdownPos] = useState({ top: 0, right: 0 });
  const toast = useToast();

  const fetchAdmins = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get('/admins');
      setAdmins(res.data?.data || []);
    } catch (error) {
      toast.error('Failed to load global admins');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchAdmins();

    const handleClickOutside = () => setActiveDropdown(null);
    const handleScroll = () => setActiveDropdown(null);
    document.addEventListener('click', handleClickOutside);
    window.addEventListener('scroll', handleScroll, true);
    return () => {
      document.removeEventListener('click', handleClickOutside);
      window.removeEventListener('scroll', handleScroll, true);
    };
  }, [fetchAdmins]);

  const toggleDropdown = (e, id) => {
    e.stopPropagation();
    if (activeDropdown === id) {
      setActiveDropdown(null);
      return;
    }
    const rect = e.currentTarget.getBoundingClientRect();
    setDropdownPos({
      top: rect.bottom + 4,
      right: window.innerWidth - rect.right,
    });
    setActiveDropdown(id);
  };

  const closeDropdown = () => setActiveDropdown(null);

  const handleDeleteAdmin = async (id) => {
    if (window.confirm('Are you sure you want to disable this admin account?')) {
      try {
        await api.delete(`/admins/${id}`);
        toast.success('Admin disabled successfully');
        fetchAdmins();
      } catch (err) {
        toast.error(err.response?.data?.error || 'Failed to disable admin');
      }
    }
  };

  const handleDemoteAdmin = async (id) => {
    if (window.confirm('Are you sure you want to demote this admin back to a regular operator?')) {
      try {
        await api.post(`/operators/${id}/admin-role`, { isGlobalAdmin: false, canAccessSettings: false, canGiveDiscount: false });
        toast.success('Admin successfully demoted to Operator');
        fetchAdmins();
      } catch (err) {
        toast.error(err.response?.data?.error || 'Failed to demote admin');
      }
    }
  };

  const handleActivateAdmin = async (id) => {
    try {
      await api.post(`/admins/${id}/activate`);
      toast.success('Admin activated successfully');
      fetchAdmins();
    } catch (err) {
      toast.error(err.response?.data?.error || 'Failed to activate admin');
    }
  };

  const handleChangeCredentials = async (e) => {
    e.preventDefault();
    const formData = new FormData(e.target);
    const newEmail = formData.get('email');
    const newPassword = formData.get('password');
    const userId = credentialsDrawer.data.id;

    if (newPassword) {
      const passwordRegex = /^(?=.*[A-Z])(?=.*\d).{8,}$/;
      if (!passwordRegex.test(newPassword)) {
        toast.error("Password must be at least 8 characters long, contain 1 uppercase letter and 1 number.");
        return;
      }
    }

    try {
      await authAPI.changeCredentials({ userId, newEmail, newPassword });
      toast.success('Credentials updated successfully.');
      setCredentialsDrawer({ isOpen: false, data: null });
      fetchAdmins();
    } catch (err) {
      toast.error(err.response?.data?.error || 'Failed to update credentials.');
    }
  };

  const AdminForm = ({ initialData, onSave }) => {
    const [permissions, setPermissions] = useState(() => {
      if (!initialData?.dashboardPermissions) return {};
      try {
        return typeof initialData.dashboardPermissions === 'string' 
          ? JSON.parse(initialData.dashboardPermissions) 
          : initialData.dashboardPermissions;
      } catch (e) {
        return {};
      }
    });

    const togglePermission = (key) => {
      setPermissions(prev => ({ ...prev, [key]: !prev[key] }));
    };

    const handleSubmit = (e) => {
      e.preventDefault();
      const formData = new FormData(e.target);
      const payload = {
        fullName: formData.get('fullName'),
        email: formData.get('email'),
        dashboardPermissions: JSON.stringify(permissions)
      };
      
      const pwd = formData.get('password');
      if (pwd) {
        const passwordRegex = /^(?=.*[A-Z])(?=.*\d).{8,}$/;
        if (!passwordRegex.test(pwd)) {
          toast.error("Password must be at least 8 characters long, contain 1 uppercase letter and 1 number.");
          return;
        }
        payload.password = pwd;
      }

      onSave(payload);
    };

    const AVAILABLE_PERMISSIONS = [
      { id: 'reports', label: 'Reports & Analytics', desc: 'Reconciliation reports and revenue data' },
      { id: 'settings', label: 'System Settings', desc: 'Configure branches and system rules' },
      { id: 'pc_status', label: 'PC Fleet Status', desc: 'Full PC network health overview' },
      // Admins have access to operators' pages by default in the branch, but we can also restrict them:
      { id: 'members', label: 'Member Management', desc: 'Manage member accounts globally' },
      { id: 'menu_editor', label: 'Menu & Pricing Profiles', desc: 'Configure cafe rates' }
    ];

    return (
      <form onSubmit={handleSubmit} className="form-stack text-xs">
        <div className="form-group">
          <label>Full Name *</label>
          <input name="fullName" required defaultValue={initialData?.fullName} className="form-control" placeholder="e.g. John Doe" />
        </div>
        
        <div className="form-group">
          <label>Email Address *</label>
          <input type="email" name="email" required defaultValue={initialData?.email} className="form-control" placeholder="admin@example.com" />
        </div>

        <div className="form-group">
          <label>{initialData ? 'Reset Password (leave blank to keep current)' : 'Password *'}</label>
          <input type="password" name="password" required={!initialData} className="form-control" placeholder="••••••••" />
        </div>

        <div className="mt-6 mb-2">
          <label className="font-semibold text-accent border-b border-border pb-1 block">Global Access Permissions</label>
          <p className="text-[10px] text-text-3 mt-1 mb-3">Select which administrative screens this user can access across all branches.</p>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3 max-h-[300px] overflow-y-auto pr-2 custom-scrollbar">
            {AVAILABLE_PERMISSIONS.map(p => (
              <label key={p.id} className={`flex items-start gap-3 p-3 rounded border cursor-pointer transition-colors ${permissions[p.id] ? 'bg-accent/10 border-accent/30' : 'bg-bg-3 border-border hover:border-border-2'}`}>
                <div className="mt-0.5">
                  <input type="checkbox" checked={!!permissions[p.id]} onChange={() => togglePermission(p.id)} className="rounded border-text-3 text-accent focus:ring-accent bg-transparent" />
                </div>
                <div>
                  <div className={`font-semibold ${permissions[p.id] ? 'text-accent' : 'text-text'}`}>{p.label}</div>
                  <div className="text-[10px] text-text-3 leading-tight mt-0.5">{p.desc}</div>
                </div>
              </label>
            ))}
          </div>
        </div>

        <div className="drawer-footer pt-4 mt-6 border-t border-border">
          <button type="submit" className="btn-primary w-full flex justify-center items-center gap-2">
            SAVE ADMIN PROFILE
          </button>
        </div>
      </form>
    );
  };

  return (
    <div className="tab-pane fade-in space-y-4">
      <div className="pane-header">
        <div>
          <h2>Global Admins</h2>
          <p className="text-text-2 text-xs mt-1">Manage global administrators who have access to all branches.</p>
        </div>
        <button 
          className="btn-primary flex items-center gap-2 text-xs shadow-lg shadow-accent/25" 
          onClick={() => setAdminDrawer({ isOpen: true, data: null })}
        >
          <Plus size={14} /> NEW ADMIN
        </button>
      </div>

      {loading ? (
        <div className="flex flex-col items-center justify-center py-16 space-y-2">
          <div className="w-6 h-6 rounded-full border-2 border-accent border-t-transparent animate-spin" />
        </div>
      ) : (
        <div className="table-container overflow-x-auto">
          <table className="data-table text-xs">
            <thead>
              <tr className="border-b border-border">
                <th>Admin Name</th>
                <th>Email Address</th>
                <th>Status</th>
                <th className="w-16"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border/60">
              {admins.map(a => (
                <tr key={a.id} className="hover:bg-bg-3/20 transition-colors">
                  <td className="font-semibold text-text">
                    {a.fullName}
                    {a.type === 'Operator' && <span className="ml-2 px-1.5 py-0.5 rounded text-[9px] bg-accent/20 text-accent font-bold uppercase">Promoted</span>}
                  </td>
                  <td className="text-text-2 font-mono">{a.email}</td>
                  <td>
                    <span className={`status-badge ${a.status?.toLowerCase()}`}>{a.status}</span>
                  </td>
                  <td className="text-right relative">
                    {a.type === 'Operator' ? (
                      <div className="flex items-center justify-end gap-3">
                        <span className="text-text-3 text-xs italic">Managed via Operators Tab</span>
                        <button 
                          className="flex items-center gap-1.5 px-3 py-1.5 bg-red-500/10 border border-red-500/20 text-red-500 hover:bg-red-500 hover:text-white rounded-md text-xs font-semibold transition-all shadow-sm"
                          onClick={() => handleDemoteAdmin(a.id)}
                        >
                          <ShieldAlert size={14} /> DEMOTE
                        </button>
                      </div>
                    ) : (
                      <>
                        <button 
                          className="icon-btn hover:text-accent" 
                          onClick={(e) => toggleDropdown(e, `admin-${a.id}`)}
                        >
                          <MoreVertical size={14} />
                        </button>
                        {activeDropdown === `admin-${a.id}` && (
                          <div className="dropdown-menu" style={{ top: dropdownPos.top, right: dropdownPos.right }}>
                            <button onClick={() => { closeDropdown(); setAdminDrawer({ isOpen: true, data: a }); }}><Edit size={12} /> Edit Profile</button>
                            <button onClick={() => { closeDropdown(); setCredentialsDrawer({ isOpen: true, data: a }); }}><KeyRound size={12} /> Change Credentials</button>
                            {a.status === 'Active' ? (
                              <button className="danger" onClick={() => { closeDropdown(); handleDeleteAdmin(a.id); }}><Trash2 size={12} /> Disable Account</button>
                            ) : (
                              <button className="text-accent hover:bg-accent/10" onClick={() => { closeDropdown(); handleActivateAdmin(a.id); }}><Check size={12} /> Enable Account</button>
                            )}
                          </div>
                        )}
                      </>
                    )}
                  </td>
                </tr>
              ))}
              {admins.length === 0 && (
                <tr>
                  <td colSpan="4" className="text-center py-8 text-text-3 font-mono">
                    No global admins found.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      <Drawer
        isOpen={adminDrawer.isOpen}
        onClose={() => setAdminDrawer({ isOpen: false, data: null })}
        title={adminDrawer.data ? 'Update Admin Profile' : 'Register Admin Account'}
        width="650px"
      >
        <AdminForm 
          initialData={adminDrawer.data} 
          onSave={async (payload) => {
            try {
              if (adminDrawer.data) {
                await api.put(`/admins/${adminDrawer.data.id}`, payload);
                toast.success('Admin profile updated');
              } else {
                await api.post('/admins', payload);
                toast.success('Admin registered successfully');
              }
              setAdminDrawer({ isOpen: false, data: null });
              fetchAdmins();
            } catch (err) {
              toast.error(err.response?.data?.error || 'Failed to save admin profile');
            }
          }}
        />
      </Drawer>

      {/* CHANGE CREDENTIALS DRAWER */}
      <Drawer
        isOpen={credentialsDrawer.isOpen}
        onClose={() => setCredentialsDrawer({ isOpen: false, data: null })}
        title={`Change Credentials: ${credentialsDrawer.data?.fullName}`}
        width="450px"
      >
        <form onSubmit={handleChangeCredentials} className="form-stack">
          <div className="mb-4 text-xs text-text-2">
            Change the email or password for this account. Leave password blank if you only want to change the email.
          </div>
          <div className="form-group">
            <label>New Email (Optional)</label>
            <input 
              type="email"
              name="email" 
              defaultValue={credentialsDrawer.data?.email || ''} 
              className="form-control" 
              placeholder="admin@example.com"
            />
          </div>
          
          <div className="form-group">
            <label>New Password (Optional)</label>
            <input 
              type="password"
              name="password" 
              className="form-control" 
              placeholder="Leave blank to keep current"
            />
          </div>
          
          <div className="drawer-footer pt-4">
            <button type="submit" className="btn-primary w-full flex justify-center items-center gap-2 font-heading tracking-wider">
              <KeyRound size={16} /> SAVE CREDENTIALS
            </button>
          </div>
        </form>
      </Drawer>
    </div>
  );
}
