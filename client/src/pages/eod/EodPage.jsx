import PageHeader from '../../components/layout/PageHeader';
import { EmptyState } from '../../components/ui/LoadingStates';

export default function EodPage() {
  return (
    <div>
      <PageHeader
        title="End of Day"
        subtitle="Daily revenue summary, shift reports, branch-level P&L"
        icon="M9 17v-2m3 2v-4m3 4v-6m2 10H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"
      />
      <div className="card">
        <EmptyState
          icon="📊"
          title="End of Day Dashboard"
          message="Complete daily revenue breakdown: gaming, food, cash, online, wallets. SOP §18 report coming next."
        />
      </div>
    </div>
  );
}
