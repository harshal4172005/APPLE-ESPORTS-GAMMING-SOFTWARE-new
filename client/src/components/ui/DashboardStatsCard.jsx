import { motion } from 'framer-motion';

export default function DashboardStatsCard({ title, value, subtitle, icon: Icon, colorClass, delay = 0 }) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 15 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, delay, ease: "easeOut" }}
      className="bg-bg-2 border border-border rounded-lg p-5 flex items-start gap-4 hover:border-border-2 transition-colors relative overflow-hidden group"
    >
      {/* Subtle hover gradient */}
      <div className={`absolute inset-0 bg-gradient-to-br from-${colorClass}/5 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-300`} />

      <div className={`p-3 rounded-md bg-${colorClass}/10 border border-${colorClass}/20`}>
        <Icon className={`w-5 h-5 text-${colorClass}`} />
      </div>
      
      <div className="flex-1 relative z-10">
        <h3 className="text-text-2 text-xs font-medium uppercase tracking-wider mb-1">{title}</h3>
        <div className="flex items-baseline gap-2">
          <motion.span 
            key={value}
            initial={{ scale: 1.1, opacity: 0.5 }}
            animate={{ scale: 1, opacity: 1 }}
            className="text-text text-2xl font-heading font-bold"
          >
            {value}
          </motion.span>
        </div>
        {subtitle && (
          <p className="text-text-3 text-[10px] mt-1">{subtitle}</p>
        )}
      </div>
    </motion.div>
  );
}
