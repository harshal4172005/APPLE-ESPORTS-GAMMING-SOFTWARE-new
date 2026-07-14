import { useState, useEffect, useCallback } from 'react';
import {
  User, Phone, Home, Briefcase, Landmark, Users,
  Search, Plus, ChevronDown, ChevronUp, Printer,
  CheckCircle2, ArrowLeft, Eye, EyeOff, FileText, Shield
} from 'lucide-react';
import api from '../../config/api';
import PageHeader from '../../components/layout/PageHeader';
import { useToast } from '../../components/ui/Toast';
import { useAuth } from '../../contexts/AuthContext';
import { useBranch } from '../../contexts/BranchContext';

// ─── Print stylesheet injected once ───────────────────────────────────────────
const printStyle = `
@media print {
  body > * { display: none !important; }
  #employee-print-root { display: block !important; }
  #employee-print-root { font-family: 'Arial', sans-serif; color: #000; padding: 30px; }
  .no-print { display: none !important; }
  .print-section { page-break-inside: avoid; margin-bottom: 20px; border: 1px solid #ccc; border-radius: 6px; padding: 14px; }
  .print-header { text-align: center; margin-bottom: 24px; border-bottom: 2px solid #000; padding-bottom: 12px; }
  .print-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
  .print-field label { font-size: 10px; font-weight: bold; text-transform: uppercase; color: #555; display: block; }
  .print-field span { font-size: 13px; font-weight: 600; color: #111; }
  .print-section h3 { font-size: 11px; font-weight: bold; text-transform: uppercase; letter-spacing: 1px; color: #333; margin-bottom: 10px; border-bottom: 1px solid #eee; padding-bottom: 6px; }
  .sig-line { border-top: 1px solid #000; width: 200px; margin-top: 40px; }
}
`;

if (!document.getElementById('emp-print-style')) {
  const s = document.createElement('style');
  s.id = 'emp-print-style';
  s.textContent = printStyle;
  document.head.appendChild(s);
}

// ─── Field helper ─────────────────────────────────────────────────────────────
const Field = ({ label, value }) => (
  <div className="print-field space-y-0.5">
    <label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block">{label}</label>
    <p className="text-sm font-semibold text-text">{value || '—'}</p>
  </div>
);

// ─── Section wrapper ──────────────────────────────────────────────────────────
const Section = ({ icon: Icon, title, color = 'text-accent', children }) => (
  <div className="bg-bg-2 border border-border rounded-xl p-5 space-y-4 print-section">
    <h3 className={`text-[11px] font-extrabold uppercase tracking-widest flex items-center gap-2 ${color}`}>
      <Icon className="w-4 h-4" /> {title}
    </h3>
    {children}
  </div>
);

const grid2 = 'grid grid-cols-1 sm:grid-cols-2 gap-4';
const grid3 = 'grid grid-cols-1 sm:grid-cols-3 gap-4';

const inputCls = 'w-full bg-bg-3 border border-border text-text text-sm rounded-lg p-2.5 focus:border-accent focus:ring-1 focus:ring-accent outline-none transition-all placeholder:text-text-3';
const selectCls = `${inputCls} cursor-pointer`;

// ─── Empty form state ─────────────────────────────────────────────────────────
const emptyForm = () => ({
  fullName: '', gender: '', dateOfBirth: '', nationality: 'Indian', maritalStatus: '',
  permanentAddress: '', currentAddress: '', phone: '', email: '',
  emergencyName: '', emergencyRelationship: '', emergencyPhone: '', emergencyEmail: '', emergencyAddress: '',
  positionTitle: '', department: '', supervisor: '', startDate: '',
  bankName: '', accountNumber: '', accountHolderName: '', bankBranch: '',
  refName: '', refRelationship: '', refPhone: '', refAddress: '',
});

// ─── VIEW: Full employee record (read-only) ───────────────────────────────────
function EmployeeDetailView({ employee, onBack }) {
  const [showBanking, setShowBanking] = useState(false);

  const handlePrint = () => window.print();

  return (
    <div>
      <div className="no-print flex items-center justify-between mb-6">
        <button onClick={onBack} className="flex items-center gap-2 text-text-2 hover:text-text text-sm font-semibold transition-colors">
          <ArrowLeft className="w-4 h-4" /> Back to Records
        </button>
        <button onClick={handlePrint} className="flex items-center gap-2 px-4 py-2 bg-accent/10 hover:bg-accent text-accent hover:text-white border border-accent/30 rounded-lg text-sm font-bold uppercase tracking-wider transition-all">
          <Printer className="w-4 h-4" /> Print / Save PDF
        </button>
      </div>

      {/* ─── Printable area ─── */}
      <div id="employee-print-root" className="space-y-4">
        <div className="print-header no-print hidden" />
        {/* Print-only header */}
        <div className="hidden print:block text-center mb-6">
          <h1 className="text-2xl font-bold">Apple Esports</h1>
          <p className="text-sm text-gray-500">Employee Joining Form — Official Record</p>
        </div>

        <div className="bg-accent/5 border border-accent/20 rounded-xl p-5 flex flex-wrap gap-6 items-center">
          <div className="w-16 h-16 rounded-full bg-accent/20 flex items-center justify-center">
            <User className="w-8 h-8 text-accent" />
          </div>
          <div>
            <p className="text-xl font-heading font-extrabold text-text">{employee.fullName}</p>
            <p className="text-sm font-mono text-accent">{employee.employeeNumber}</p>
            <p className="text-xs text-text-3 mt-0.5">{employee.positionTitle || 'Position not set'} · {employee.branchName}</p>
          </div>
          <span className={`ml-auto px-3 py-1 rounded-full text-xs font-bold uppercase tracking-wider border ${
            employee.status === 'Active' ? 'bg-neon-green/10 text-neon-green border-neon-green/30' : 'bg-neon-orange/10 text-neon-orange border-neon-orange/30'
          }`}>{employee.status}</span>
        </div>

        <Section icon={User} title="Personal Information" color="text-neon-purple">
          <div className={grid2}>
            <Field label="Full Name" value={employee.fullName} />
            <Field label="Gender" value={employee.gender} />
            <Field label="Date of Birth" value={employee.dateOfBirth} />
            <Field label="Nationality" value={employee.nationality} />
            <Field label="Marital Status" value={employee.maritalStatus} />
          </div>
        </Section>

        <Section icon={Phone} title="Contact Information" color="text-neon-blue">
          <div className={grid2}>
            <Field label="Phone" value={employee.phone} />
            <Field label="Email" value={employee.email} />
            <Field label="Permanent Address" value={employee.permanentAddress} />
            <Field label="Current Address" value={employee.currentAddress} />
          </div>
        </Section>

        <Section icon={Shield} title="Emergency Contact" color="text-neon-orange">
          <div className={grid2}>
            <Field label="Name" value={employee.emergencyName} />
            <Field label="Relationship" value={employee.emergencyRelationship} />
            <Field label="Phone" value={employee.emergencyPhone} />
            <Field label="Email" value={employee.emergencyEmail} />
            <Field label="Address" value={employee.emergencyAddress} />
          </div>
        </Section>

        <Section icon={Briefcase} title="Job Information" color="text-accent">
          <div className={grid2}>
            <Field label="Position Title" value={employee.positionTitle} />
            <Field label="Department" value={employee.department} />
            <Field label="Supervisor / Manager" value={employee.supervisor} />
            <Field label="Start Date" value={employee.startDate} />
          </div>
        </Section>

        {/* Banking — hidden unless toggled on screen, always visible on print */}
        <div>
          <button
            onClick={() => setShowBanking(v => !v)}
            className="no-print flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-text-2 hover:text-text mb-2 transition-colors"
          >
            {showBanking ? <EyeOff className="w-3.5 h-3.5" /> : <Eye className="w-3.5 h-3.5" />}
            {showBanking ? 'Hide' : 'Show'} Banking Information
          </button>
          <div className={`${!showBanking ? 'no-print' : ''}`}>
            <Section icon={Landmark} title="Banking Information" color="text-neon-red">
              <div className={grid2}>
                <Field label="Bank Name" value={employee.bankName} />
                <Field label="Account Number" value={showBanking ? employee.accountNumber : '••••••••'} />
                <Field label="Account Holder Name" value={employee.accountHolderName} />
                <Field label="Bank Branch" value={employee.bankBranch} />
              </div>
            </Section>
          </div>
        </div>

        <Section icon={Users} title="Reference" color="text-text-2">
          <div className={grid2}>
            <Field label="Reference Name" value={employee.refName} />
            <Field label="Relationship" value={employee.refRelationship} />
            <Field label="Phone" value={employee.refPhone} />
            <Field label="Address" value={employee.refAddress} />
          </div>
        </Section>

        {/* Declaration */}
        <div className="bg-bg-2 border border-border rounded-xl p-5 space-y-3 print-section">
          <h3 className="text-[11px] font-extrabold uppercase tracking-widest text-text-3">Declaration</h3>
          <p className="text-xs text-text-2 leading-relaxed">
            I hereby declare that all the information provided by me to the firm is true, complete, and accurate to the best of my knowledge. I accept full responsibility for the correctness of the details submitted. If any information provided by me is found to be false, incorrect, or misleading, I shall be solely responsible for any consequences arising from it, and the firm shall not be held liable in any manner.
          </p>
          <div className="flex gap-16 mt-4">
            <div>
              <div className="sig-line w-40 border-t border-border mt-8" />
              <p className="text-[10px] text-text-3 mt-1">Employee Signature</p>
            </div>
            <div>
              <p className="text-xs text-text font-mono mt-6">{employee.startDate || '___________'}</p>
              <div className="sig-line w-40 border-t border-border mt-1" />
              <p className="text-[10px] text-text-3 mt-1">Date</p>
            </div>
          </div>
          <p className="text-[10px] text-text-3 mt-4 border-t border-border pt-3">
            Submitted by: <span className="text-text font-semibold">{employee.submittedByName || 'System'}</span> · 
            Registered: <span className="text-text font-semibold">{new Date(employee.createdAt).toLocaleString()}</span>
          </p>
        </div>
      </div>
    </div>
  );
}

// ─── Main Page ────────────────────────────────────────────────────────────────
export default function EmployeeFormsPage() {
  const { isSuperAdmin, user } = useAuth();
  const { activeBranch } = useBranch();
  const toast = useToast();

  const [tab, setTab] = useState('records'); // 'records' | 'new'
  const [employees, setEmployees] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [selectedEmployee, setSelectedEmployee] = useState(null);
  const [form, setForm] = useState(emptyForm());
  const [isSubmitting, setIsSubmitting] = useState(false);

  const targetBranchId = isSuperAdmin ? activeBranch?.id : user?.branchId;

  const fetchEmployees = useCallback(async () => {
    if (!targetBranchId) return;
    try {
      setIsLoading(true);
      const res = await api.get('/employees', { params: { search: search || undefined, page: 1, pageSize: 100 } });
      setEmployees(res.data.data?.items || []);
    } catch {
      toast.error('Failed to load employee records');
    } finally {
      setIsLoading(false);
    }
  }, [targetBranchId, search]);

  useEffect(() => { fetchEmployees(); }, [fetchEmployees]);

  const set = (field) => (e) => setForm(f => ({ ...f, [field]: e.target.value }));

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!form.fullName.trim()) { toast.error('Full Name is required'); return; }
    try {
      setIsSubmitting(true);
      const payload = {
        fullName: form.fullName, gender: form.gender || null,
        dateOfBirth: form.dateOfBirth || null, nationality: form.nationality || 'Indian',
        maritalStatus: form.maritalStatus || null,
        permanentAddress: form.permanentAddress || null, currentAddress: form.currentAddress || null,
        phone: form.phone || null, email: form.email || null,
        emergencyName: form.emergencyName || null, emergencyRelationship: form.emergencyRelationship || null,
        emergencyPhone: form.emergencyPhone || null, emergencyEmail: form.emergencyEmail || null,
        emergencyAddress: form.emergencyAddress || null,
        positionTitle: form.positionTitle || null, department: form.department || null,
        supervisor: form.supervisor || null, startDate: form.startDate || null,
        bankName: form.bankName || null, accountNumber: form.accountNumber || null,
        accountHolderName: form.accountHolderName || null, bankBranch: form.bankBranch || null,
        refName: form.refName || null, refRelationship: form.refRelationship || null,
        refPhone: form.refPhone || null, refAddress: form.refAddress || null,
      };
      await api.post('/employees', payload);
      toast.success('Employee record created successfully!');
      setForm(emptyForm());
      setTab('records');
      fetchEmployees();
    } catch (err) {
      toast.error(err.response?.data?.error || 'Failed to submit form');
    } finally {
      setIsSubmitting(false);
    }
  };

  if (isSuperAdmin && !activeBranch) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[60vh] text-center">
        <FileText className="w-12 h-12 text-text-3 mb-4" />
        <h2 className="text-xl font-heading font-bold text-text mb-2">Select a Branch</h2>
        <p className="text-text-2">You must select a branch to view or add employees.</p>
      </div>
    );
  }

  if (selectedEmployee) {
    return (
      <div className="h-full flex flex-col">
        <PageHeader title="Employee Record" subtitle={selectedEmployee.employeeNumber} icon="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
        <div className="flex-1 overflow-y-auto mt-6">
          <EmployeeDetailView employee={selectedEmployee} onBack={() => setSelectedEmployee(null)} />
        </div>
      </div>
    );
  }

  return (
    <div className="h-full flex flex-col">
      <PageHeader
        title="Employee Forms"
        subtitle="Digital HR joining forms — Admin & SuperAdmin only"
        icon="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"
        badge="HR"
      />

      {/* Tabs */}
      <div className="flex items-center gap-2 mt-6 border-b border-border pb-2">
        <button onClick={() => setTab('records')} className={`px-4 py-2 text-sm font-bold uppercase tracking-wider rounded-t-lg border-b-2 transition-all ${tab === 'records' ? 'border-accent text-accent bg-accent/5' : 'border-transparent text-text-3 hover:text-text-2'}`}>
          Employee Records
        </button>
        <button onClick={() => setTab('new')} className={`px-4 py-2 text-sm font-bold uppercase tracking-wider rounded-t-lg border-b-2 transition-all flex items-center gap-1.5 ${tab === 'new' ? 'border-neon-green text-neon-green bg-neon-green/5' : 'border-transparent text-text-3 hover:text-text-2'}`}>
          <Plus className="w-3.5 h-3.5" /> New Joining Form
        </button>
      </div>

      <div className="flex-1 overflow-y-auto mt-5">

        {/* ── Records Tab ── */}
        {tab === 'records' && (
          <div className="space-y-4">
            <div className="relative max-w-sm">
              <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-text-3" />
              <input
                type="text"
                placeholder="Search by name, phone, or ID..."
                value={search}
                onChange={e => setSearch(e.target.value)}
                className="pl-9 pr-4 py-2 text-sm bg-bg-2 border border-border rounded-lg text-text focus:outline-none focus:border-accent w-full transition-colors"
              />
            </div>

            {isLoading ? (
              <div className="flex justify-center items-center py-16">
                <span className="w-8 h-8 border-2 border-accent border-t-transparent rounded-full animate-spin" />
              </div>
            ) : employees.length === 0 ? (
              <div className="text-center py-16 bg-bg-2 border border-border rounded-xl">
                <FileText className="w-12 h-12 mx-auto mb-3 text-text-3 opacity-30" />
                <p className="text-sm font-bold uppercase tracking-wider text-text-3">No employee records found</p>
                <button onClick={() => setTab('new')} className="mt-4 px-4 py-2 bg-accent/10 hover:bg-accent text-accent hover:text-white border border-accent/30 rounded-lg text-sm font-bold transition-all">
                  Add First Employee
                </button>
              </div>
            ) : (
              <div className="bg-bg-2 border border-border rounded-xl overflow-x-auto shadow-sm">
                <table className="w-full text-left border-collapse whitespace-nowrap text-sm">
                  <thead>
                    <tr className="bg-bg-3/50 border-b border-border text-[10px] text-text-3 font-bold uppercase tracking-wider">
                      <th className="p-4">Employee ID</th>
                      <th className="p-4">Name</th>
                      <th className="p-4">Position</th>
                      <th className="p-4">Department</th>
                      <th className="p-4">Phone</th>
                      <th className="p-4">Start Date</th>
                      <th className="p-4">Status</th>
                      <th className="p-4 text-right">Action</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border/50">
                    {employees.map(emp => (
                      <tr key={emp.id} className="hover:bg-bg-3/30 transition-colors">
                        <td className="p-4 font-mono text-accent text-xs">{emp.employeeNumber}</td>
                        <td className="p-4 font-bold text-text flex items-center gap-2">
                          <div className="w-7 h-7 rounded-full bg-accent/10 flex items-center justify-center text-accent text-xs font-bold">
                            {emp.fullName.charAt(0)}
                          </div>
                          {emp.fullName}
                        </td>
                        <td className="p-4 text-text-2">{emp.positionTitle || '—'}</td>
                        <td className="p-4 text-text-2">{emp.department || '—'}</td>
                        <td className="p-4 text-text-2 font-mono">{emp.phone || '—'}</td>
                        <td className="p-4 text-text-2">{emp.startDate || '—'}</td>
                        <td className="p-4">
                          <span className={`text-[10px] font-bold px-2 py-1 rounded border uppercase ${
                            emp.status === 'Active' ? 'bg-neon-green/10 text-neon-green border-neon-green/30' : 'bg-neon-orange/10 text-neon-orange border-neon-orange/30'
                          }`}>{emp.status}</span>
                        </td>
                        <td className="p-4 text-right">
                          <button onClick={() => setSelectedEmployee(emp)} className="px-3 py-1.5 bg-accent/10 hover:bg-accent text-accent hover:text-white border border-accent/30 rounded-lg text-[11px] font-bold uppercase tracking-wider transition-all">
                            View
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}

        {/* ── New Form Tab ── */}
        {tab === 'new' && (
          <form onSubmit={handleSubmit} className="space-y-5 pb-10">
            {/* Personal */}
            <Section icon={User} title="Personal Information" color="text-neon-purple">
              <div className={grid2}>
                <div className="sm:col-span-2">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Full Name *</label>
                  <input required value={form.fullName} onChange={set('fullName')} placeholder="Full legal name" className={inputCls} />
                </div>
                <div>
                  <label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Gender</label>
                  <select value={form.gender} onChange={set('gender')} className={selectCls}>
                    <option value="">Select...</option>
                    <option>Male</option><option>Female</option><option>Other</option>
                  </select>
                </div>
                <div>
                  <label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Date of Birth</label>
                  <input type="date" value={form.dateOfBirth} onChange={set('dateOfBirth')} className={inputCls} style={{ colorScheme: 'dark' }} />
                </div>
                <div>
                  <label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Nationality</label>
                  <select value={form.nationality} onChange={set('nationality')} className={selectCls}>
                    <option>Indian</option><option>Other</option>
                  </select>
                </div>
                <div>
                  <label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Marital Status</label>
                  <select value={form.maritalStatus} onChange={set('maritalStatus')} className={selectCls}>
                    <option value="">Select...</option>
                    <option>Unmarried</option><option>Married</option>
                  </select>
                </div>
              </div>
            </Section>

            {/* Contact */}
            <Section icon={Phone} title="Contact Information" color="text-neon-blue">
              <div className={grid2}>
                <div>
                  <label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Phone Number</label>
                  <input value={form.phone} onChange={set('phone')} placeholder="+91-XXXXXXXXXX" className={inputCls} />
                </div>
                <div>
                  <label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Email Address</label>
                  <input type="email" value={form.email} onChange={set('email')} placeholder="email@example.com" className={inputCls} />
                </div>
                <div className="sm:col-span-2">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Permanent Address</label>
                  <textarea rows={2} value={form.permanentAddress} onChange={set('permanentAddress')} placeholder="Full permanent address" className={inputCls} />
                </div>
                <div className="sm:col-span-2">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Current Address</label>
                  <textarea rows={2} value={form.currentAddress} onChange={set('currentAddress')} placeholder="Current residential address" className={inputCls} />
                </div>
              </div>
            </Section>

            {/* Emergency Contact */}
            <Section icon={Shield} title="Emergency Contact" color="text-neon-orange">
              <div className={grid2}>
                <div><label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Contact Name</label><input value={form.emergencyName} onChange={set('emergencyName')} placeholder="Emergency contact name" className={inputCls} /></div>
                <div><label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Relationship</label><input value={form.emergencyRelationship} onChange={set('emergencyRelationship')} placeholder="e.g. Father, Spouse" className={inputCls} /></div>
                <div><label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Phone</label><input value={form.emergencyPhone} onChange={set('emergencyPhone')} placeholder="+91-XXXXXXXXXX" className={inputCls} /></div>
                <div><label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Email</label><input type="email" value={form.emergencyEmail} onChange={set('emergencyEmail')} placeholder="emergency@email.com" className={inputCls} /></div>
                <div className="sm:col-span-2"><label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Address</label><textarea rows={2} value={form.emergencyAddress} onChange={set('emergencyAddress')} placeholder="Emergency contact address" className={inputCls} /></div>
              </div>
            </Section>

            {/* Job */}
            <Section icon={Briefcase} title="Job Information" color="text-accent">
              <div className={grid2}>
                <div><label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Position Title</label><input value={form.positionTitle} onChange={set('positionTitle')} placeholder="e.g. Gaming Operator" className={inputCls} /></div>
                <div>
                  <label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Department / Branch</label>
                  <select value={form.department} onChange={set('department')} className={selectCls}>
                    <option value="">Select Branch...</option>
                    <option>Adajan Branch</option><option>Citylight Branch</option>
                    <option>Katargam Branch</option><option>Varaccha Branch</option>
                  </select>
                </div>
                <div><label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Supervisor / Manager</label><input value={form.supervisor} onChange={set('supervisor')} placeholder="Reporting manager name" className={inputCls} /></div>
                <div><label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Start Date</label><input type="date" value={form.startDate} onChange={set('startDate')} className={inputCls} style={{ colorScheme: 'dark' }} /></div>
              </div>
            </Section>

            {/* Banking */}
            <Section icon={Landmark} title="Banking Information" color="text-neon-red">
              <p className="text-[10px] text-text-3 -mt-2 mb-2 flex items-center gap-1"><EyeOff className="w-3 h-3" /> Banking details are stored securely and hidden from screen by default.</p>
              <div className={grid2}>
                <div><label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Bank Name</label><input value={form.bankName} onChange={set('bankName')} placeholder="e.g. State Bank of India" className={inputCls} /></div>
                <div><label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Account Number</label><input value={form.accountNumber} onChange={set('accountNumber')} placeholder="Account number" className={inputCls} /></div>
                <div><label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Account Holder Name</label><input value={form.accountHolderName} onChange={set('accountHolderName')} placeholder="Name as per bank records" className={inputCls} /></div>
                <div><label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Bank Branch / IFSC</label><input value={form.bankBranch} onChange={set('bankBranch')} placeholder="Branch name or IFSC code" className={inputCls} /></div>
              </div>
            </Section>

            {/* Reference */}
            <Section icon={Users} title="Reference" color="text-text-2">
              <div className={grid2}>
                <div><label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Reference Name</label><input value={form.refName} onChange={set('refName')} placeholder="Reference person name" className={inputCls} /></div>
                <div><label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Relationship</label><input value={form.refRelationship} onChange={set('refRelationship')} placeholder="e.g. Former Manager" className={inputCls} /></div>
                <div><label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Phone</label><input value={form.refPhone} onChange={set('refPhone')} placeholder="+91-XXXXXXXXXX" className={inputCls} /></div>
                <div><label className="text-[10px] font-bold uppercase tracking-widest text-text-3 block mb-1">Address</label><input value={form.refAddress} onChange={set('refAddress')} placeholder="Reference address" className={inputCls} /></div>
              </div>
            </Section>

            {/* Declaration */}
            <div className="bg-bg-2 border border-neon-orange/30 rounded-xl p-5 space-y-3">
              <h3 className="text-[11px] font-extrabold uppercase tracking-widest text-neon-orange flex items-center gap-2">
                <CheckCircle2 className="w-4 h-4" /> Declaration
              </h3>
              <p className="text-xs text-text-2 leading-relaxed">
                By submitting this form, the employee declares that all information provided is true, complete, and accurate. Any false information will be their sole responsibility. They confirm having read and accepted all terms and conditions provided by Apple Esports.
              </p>
            </div>

            {/* Submit */}
            <div className="flex gap-3 justify-end pt-2">
              <button type="button" onClick={() => { setForm(emptyForm()); setTab('records'); }}
                className="px-6 py-2.5 border border-border rounded-lg text-sm text-text-2 hover:text-text hover:border-text-2 transition-all font-semibold">
                Cancel
              </button>
              <button type="submit" disabled={isSubmitting}
                className="px-8 py-2.5 bg-accent hover:bg-accent/90 text-white rounded-lg text-sm font-bold uppercase tracking-wider transition-all disabled:opacity-50 flex items-center gap-2">
                {isSubmitting ? <span className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" /> : <CheckCircle2 className="w-4 h-4" />}
                Submit Joining Form
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
