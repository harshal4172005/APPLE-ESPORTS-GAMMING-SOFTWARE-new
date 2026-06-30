import { Link } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';

export default function UnauthorizedPage() {
  const { user } = useAuth();

  return (
    <div className="min-h-screen bg-bg flex items-center justify-center p-4">
      <div className="card max-w-md w-full text-center p-8">
        <div className="w-16 h-16 bg-neon-red/10 border border-neon-red/20 rounded-full flex items-center justify-center mx-auto mb-4">
          <span className="text-3xl">🚫</span>
        </div>
        <h2 className="font-heading text-xl font-bold text-neon-red mb-2">Access Denied</h2>
        <p className="text-text-2 text-sm mb-6">
          You don't have permission to access this page.
          {user?.role === 'operator' && ' Contact your Super Admin to request access.'}
        </p>
        <Link to="/app/billing" className="btn-primary inline-block">
          ← Return to Billing Counter
        </Link>
      </div>
    </div>
  );
}
