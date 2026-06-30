// ═══════════════════════════════════════════════════════════
// Gaming Café ERP — Loading States
// Skeleton loaders and spinners for async content
// ═══════════════════════════════════════════════════════════

/**
 * Full-page loading spinner
 */
export function PageLoader({ message = 'Loading...' }) {
  return (
    <div className="flex flex-col items-center justify-center py-20">
      <div className="w-10 h-10 border-2 border-accent/20 border-t-accent rounded-full animate-spin mb-4" />
      <p className="text-text-2 text-sm font-mono">{message}</p>
    </div>
  );
}

/**
 * Inline spinner for buttons and small areas
 */
export function Spinner({ size = 'md', className = '' }) {
  const sizeMap = {
    sm: 'w-4 h-4 border',
    md: 'w-6 h-6 border-2',
    lg: 'w-8 h-8 border-2',
  };

  return (
    <div className={`${sizeMap[size]} border-accent/20 border-t-accent rounded-full animate-spin ${className}`} />
  );
}

/**
 * Skeleton card loader
 */
export function SkeletonCard({ count = 1 }) {
  return (
    <>
      {Array.from({ length: count }).map((_, i) => (
        <div key={i} className="card animate-pulse">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-8 h-8 bg-bg-3 rounded-md" />
            <div className="flex-1">
              <div className="h-3 bg-bg-3 rounded w-1/3 mb-2" />
              <div className="h-2 bg-bg-3 rounded w-1/2" />
            </div>
          </div>
          <div className="space-y-2">
            <div className="h-2.5 bg-bg-3 rounded w-full" />
            <div className="h-2.5 bg-bg-3 rounded w-4/5" />
            <div className="h-2.5 bg-bg-3 rounded w-3/5" />
          </div>
        </div>
      ))}
    </>
  );
}

/**
 * Skeleton stats row
 */
export function SkeletonStats({ count = 4 }) {
  return (
    <div className={`grid grid-cols-2 sm:grid-cols-${count} gap-3`}>
      {Array.from({ length: count }).map((_, i) => (
        <div key={i} className="stat-card animate-pulse">
          <div className="h-2 bg-bg-3 rounded w-1/2 mb-2" />
          <div className="h-5 bg-bg-3 rounded w-2/3" />
        </div>
      ))}
    </div>
  );
}

/**
 * Skeleton grid (for PC grid, menu items, etc.)
 */
export function SkeletonGrid({ cols = 5, rows = 4 }) {
  return (
    <div className={`grid grid-cols-${cols} gap-2`}>
      {Array.from({ length: cols * rows }).map((_, i) => (
        <div key={i} className="bg-bg-3 rounded-sm h-16 animate-pulse" />
      ))}
    </div>
  );
}

/**
 * Empty state
 */
export function EmptyState({ icon, title, message, action }) {
  return (
    <div className="flex flex-col items-center justify-center py-16 text-center">
      {icon && (
        <div className="w-16 h-16 bg-bg-3 rounded-full flex items-center justify-center mb-4 opacity-30">
          <span className="text-3xl">{icon}</span>
        </div>
      )}
      <h3 className="font-heading text-base font-semibold text-text mb-1">{title}</h3>
      <p className="text-text-2 text-xs max-w-xs">{message}</p>
      {action && <div className="mt-4">{action}</div>}
    </div>
  );
}
