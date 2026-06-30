import PcCard from './PcCard';

export default function PcGrid({ pcs, walkinRequests, onStartSession, onRefresh, onStartReservedSession, onOverrideReservation, onApproveWalkin, onDeclineWalkin, onFlagMaintenance }) {
  if (!pcs || pcs.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center p-12 text-center bg-bg-2 border border-border rounded-lg">
        <p className="text-text-2 text-lg">No PCs detected for this branch.</p>
        <p className="text-text-3 text-sm mt-2">Add PCs via the Super Admin Settings.</p>
      </div>
    );
  }

  return (
    <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 xl:grid-cols-4 gap-3">
      {pcs.map((pc) => {
        const walkinReq = walkinRequests?.find(r => r.pcId === pc.name || r.pcId === pc.id);
        return (
          <PcCard
            key={pc.id}
            pc={pc}
            walkinReq={walkinReq}
            onStartSession={onStartSession}
            onRefresh={onRefresh}
            onStartReservedSession={onStartReservedSession}
            onOverrideReservation={onOverrideReservation}
            onApproveWalkin={onApproveWalkin}
            onDeclineWalkin={onDeclineWalkin}
            onFlagMaintenance={onFlagMaintenance}
          />
        );
      })}
    </div>
  );
}
