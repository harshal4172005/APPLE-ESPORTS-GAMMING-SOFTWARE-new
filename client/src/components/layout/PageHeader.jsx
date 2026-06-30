// ═══════════════════════════════════════════════════════════
// Gaming Café ERP — Page Header Component
// Reusable header for every dashboard page
// ═══════════════════════════════════════════════════════════

export default function PageHeader({ title, subtitle, icon, badge, actions }) {
  return (
    <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 mb-4">
      <div className="flex items-center gap-3">
        {icon && (
          <div className="w-9 h-9 bg-accent/8 border border-accent/20 rounded-md flex items-center justify-center text-accent flex-shrink-0">
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth={1.8}>
              <path strokeLinecap="round" strokeLinejoin="round" d={icon} />
            </svg>
          </div>
        )}
        <div>
          <h1 className="font-heading text-lg font-bold text-text tracking-wide leading-tight flex items-center gap-2">
            {title}
            {badge && (
              <span className="text-[9px] font-mono font-semibold tracking-wider px-2 py-0.5 rounded-full bg-accent/10 border border-accent/30 text-accent">
                {badge}
              </span>
            )}
          </h1>
          {subtitle && (
            <p className="text-[11px] text-text-2 mt-0.5">{subtitle}</p>
          )}
        </div>
      </div>
      {actions && (
        <div className="flex items-center gap-2 flex-shrink-0">
          {actions}
        </div>
      )}
    </div>
  );
}
