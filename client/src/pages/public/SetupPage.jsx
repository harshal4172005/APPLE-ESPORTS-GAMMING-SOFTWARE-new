import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ShieldAlert, MonitorStop, ArrowLeft, Eye, EyeOff } from 'lucide-react';
import { authAPI } from '../../api/auth.api';

export default function SetupPage() {
  const { role } = useParams(); // 'superadmin' or 'operator'
  const navigate = useNavigate();
  
  const [formData, setFormData] = useState({
    fullName: '',
    email: '',
    username: '',
    password: '',
    branchId: ''
  });
  
  const [branches, setBranches] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [showPassword, setShowPassword] = useState(false);

  useEffect(() => {
    if (role === 'operator') {
      authAPI.getBranches()
        .then(res => setBranches(res.data.data))
        .catch(err => console.error('Failed to load branches', err));
    }
  }, [role]);

  const handleChange = (e) => {
    setFormData({ ...formData, [e.target.name]: e.target.value });
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);
    setError(null);

    // Validate password complexity
    const passwordRegex = /^(?=.*[A-Z])(?=.*\d).{8,}$/;
    if (!passwordRegex.test(formData.password)) {
      setError("Password must be at least 8 characters long, contain 1 uppercase letter and 1 number.");
      setLoading(false);
      return;
    }

    try {
      if (role === 'superadmin') {
        await authAPI.setupMaster({
          fullName: formData.fullName,
          email: formData.email,
          password: formData.password
        });
        navigate('/login/superadmin', { state: { message: "Setup complete! Please login." } });
      } else if (role === 'operator') {
        if (!formData.branchId) {
          setError("Please select a branch.");
          setLoading(false);
          return;
        }
        await authAPI.setupOperator(formData);
        navigate('/login/operator', { state: { message: "Setup complete! Please login." } });
      }
    } catch (err) {
      setError(err.response?.data?.error || "Setup failed. Please try again.");
    } finally {
      setLoading(false);
    }
  };

  const isOperator = role === 'operator';

  return (
    <div className="min-h-screen bg-bg flex items-center justify-center p-6 relative overflow-hidden">
      <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[600px] bg-accent/20 rounded-full blur-[120px] pointer-events-none" />
      
      <div className="relative z-10 w-full max-w-md bg-bg-2/80 backdrop-blur-xl border border-border/60 p-8 shadow-2xl rounded-lg">
        <button 
          onClick={() => navigate('/')}
          className="flex items-center text-text-2 hover:text-accent transition-colors mb-6 text-sm"
        >
          <ArrowLeft className="w-4 h-4 mr-2" />
          Back to Gateway
        </button>

        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-16 h-16 rounded-full bg-accent/20 mb-4">
            {isOperator ? <MonitorStop className="w-8 h-8 text-accent" /> : <ShieldAlert className="w-8 h-8 text-accent" />}
          </div>
          <h2 className="font-heading text-2xl font-bold text-text uppercase tracking-wider">
            {isOperator ? "Operator Setup" : "Master Setup"}
          </h2>
          <p className="text-text-2 text-sm mt-2">
            Register the first {isOperator ? "operator" : "super admin"} account.
          </p>
        </div>

        {error && (
          <div className="bg-red-500/10 border border-red-500/50 text-red-500 text-sm p-3 rounded mb-6 text-center">
            {error}
          </div>
        )}

        {isOperator && branches.length === 0 && !loading && (
           <div className="bg-yellow-500/10 border border-yellow-500/50 text-yellow-500 text-sm p-3 rounded mb-6 text-center">
            No branches found. Please setup Super Admin and create a branch first.
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-xs font-mono text-text-2 mb-1 uppercase tracking-wider">Full Name</label>
            <input 
              type="text"
              name="fullName"
              required
              value={formData.fullName}
              onChange={handleChange}
              className="w-full bg-bg border border-border rounded p-3 text-text focus:outline-none focus:border-accent transition-colors"
              placeholder="Enter your full name"
            />
          </div>

          <div>
            <label className="block text-xs font-mono text-text-2 mb-1 uppercase tracking-wider">Email Address</label>
            <input 
              type="email"
              name="email"
              required
              value={formData.email}
              onChange={handleChange}
              className="w-full bg-bg border border-border rounded p-3 text-text focus:outline-none focus:border-accent transition-colors"
              placeholder="admin@example.com"
            />
          </div>

          {isOperator && (
            <div>
              <label className="block text-xs font-mono text-text-2 mb-1 uppercase tracking-wider">Username</label>
              <input 
                type="text"
                name="username"
                required
                value={formData.username}
                onChange={handleChange}
                className="w-full bg-bg border border-border rounded p-3 text-text focus:outline-none focus:border-accent transition-colors"
                placeholder="Choose a username"
              />
            </div>
          )}

          {isOperator && branches.length > 0 && (
            <div>
              <label className="block text-xs font-mono text-text-2 mb-1 uppercase tracking-wider">Assign Branch</label>
              <select
                name="branchId"
                required
                value={formData.branchId}
                onChange={handleChange}
                className="w-full bg-bg border border-border rounded p-3 text-text focus:outline-none focus:border-accent transition-colors"
              >
                <option value="">Select a branch</option>
                {branches.map(b => (
                  <option key={b.id} value={b.id}>{b.name}</option>
                ))}
              </select>
            </div>
          )}

          <div className="relative">
            <label className="block text-xs font-mono text-text-2 mb-1 uppercase tracking-wider">Password</label>
            <input 
              type={showPassword ? "text" : "password"}
              name="password"
              required
              value={formData.password}
              onChange={handleChange}
              className="w-full bg-bg border border-border rounded p-3 text-text focus:outline-none focus:border-accent transition-colors"
              placeholder="Must contain 8 chars, 1 uppercase, 1 number"
            />
            <button
              type="button"
              onClick={() => setShowPassword(!showPassword)}
              className="absolute right-3 top-9 text-text-2 hover:text-text transition-colors"
            >
              {showPassword ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
            </button>
          </div>

          <button
            type="submit"
            disabled={loading || (isOperator && branches.length === 0)}
            className="w-full bg-accent hover:bg-red-700 text-white font-bold py-3 rounded transition-colors uppercase tracking-widest mt-6 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {loading ? "Processing..." : "Finish Setup"}
          </button>
        </form>
      </div>
    </div>
  );
}
