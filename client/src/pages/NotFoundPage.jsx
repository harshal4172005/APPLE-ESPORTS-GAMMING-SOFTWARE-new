import { Link } from 'react-router-dom';

export default function NotFoundPage() {
  return (
    <div className="min-h-screen bg-bg flex items-center justify-center p-4">
      <div className="card max-w-md w-full text-center p-8">
        <div className="font-heading text-6xl font-bold text-text-3 mb-2">404</div>
        <h2 className="font-heading text-lg font-bold text-text mb-2">Page Not Found</h2>
        <p className="text-text-2 text-sm mb-6">
          The page you're looking for doesn't exist or has been moved.
        </p>
        <Link to="/app/billing" className="btn-primary inline-block">
          ← Return to Billing Counter
        </Link>
      </div>
    </div>
  );
}
