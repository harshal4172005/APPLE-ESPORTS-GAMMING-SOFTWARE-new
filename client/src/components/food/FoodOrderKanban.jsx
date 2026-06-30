import { memo } from 'react';
import FoodOrderCard from './FoodOrderCard';
import { ChefHat, Coffee, CheckCircle2, ArrowRightCircle } from 'lucide-react';

const KanbanColumn = memo(({ title, icon: Icon, orders, colorClass, onOrderUpdated }) => {
  return (
    <div className="flex flex-col bg-bg-2 border border-border rounded-xl h-full overflow-hidden">
      <div className={`p-4 border-b border-border bg-bg-3 flex justify-between items-center ${colorClass}`}>
        <h3 className="font-heading font-bold uppercase tracking-wider flex items-center gap-2">
          <Icon className="w-5 h-5" />
          {title}
        </h3>
        <span className="bg-bg-4 text-text-2 px-2.5 py-0.5 rounded-full text-xs font-bold font-mono">
          {orders.length}
        </span>
      </div>
      
      <div className="flex-1 overflow-y-auto p-3 space-y-3">
        {orders.length === 0 ? (
          <div className="text-center text-text-3 text-xs italic py-8">
            No orders
          </div>
        ) : (
          orders.map(order => (
            <FoodOrderCard 
              key={order.id} 
              order={order} 
              onOrderUpdated={onOrderUpdated} 
            />
          ))
        )}
      </div>
    </div>
  );
});

KanbanColumn.displayName = 'KanbanColumn';

export default function FoodOrderKanban({ orders, onOrderUpdated }) {
  // Enums mapped to 0, 1, 2, 3, etc. or strings. Assuming they come back as numbers or strings matching Enum names.
  const pendingOrders = orders.filter(o => o.status === 0 || o.status === 'Pending');
  const preparingOrders = orders.filter(o => o.status === 1 || o.status === 'Preparing');
  const readyOrders = orders.filter(o => o.status === 2 || o.status === 'Ready');
  const deliveredOrders = orders.filter(o => o.status === 3 || o.status === 'Delivered');

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 gap-4 h-full pb-4">
      <KanbanColumn 
        title="Pending" 
        icon={Coffee} 
        orders={pendingOrders} 
        colorClass="text-neon-orange border-b-neon-orange/20"
        onOrderUpdated={onOrderUpdated} 
      />
      <KanbanColumn 
        title="Delivered" 
        icon={CheckCircle2} 
        orders={deliveredOrders} 
        colorClass="text-accent border-b-accent/20"
        onOrderUpdated={onOrderUpdated} 
      />
    </div>
  );
}
