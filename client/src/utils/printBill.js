import api from '../config/api';

export const printBill = async (billId, fullBillObject = null) => {
  try {
    let idString = billId.toString();

    if (idString === 'SETTLED-CREDIT') {
      alert('This is a manual credit clearance without an associated bill. It cannot be printed.');
      return;
    }

    if (idString.startsWith('SETTLED-')) {
      if (!fullBillObject) {
        alert('Cannot print credit settlement without full row data.');
        return;
      }
      
      const printWindow = window.open('', '_blank', 'width=800,height=600');
      if (!printWindow) {
        alert('Please allow popups to print bills.');
        return;
      }

      const html = `
        <!DOCTYPE html>
        <html>
          <head>
            <title>Credit Settlement</title>
            <style>
              body { 
                font-family: 'Courier New', Courier, monospace; 
                width: 80mm; 
                margin: 0 auto; 
                padding: 10px;
                color: black; 
                background: white; 
                font-size: 12px; 
              }
              .text-center { text-align: center; }
              .flex { display: flex; justify-content: space-between; }
              .border-b { border-bottom: 1px dashed black; padding-bottom: 5px; margin-bottom: 5px; }
              .font-bold { font-weight: bold; }
              @media print {
                body { margin: 0; padding: 0; }
                @page { margin: 0; }
              }
            </style>
          </head>
          <body>
            <div class="text-center border-b">
              <h2 style="margin:0; font-size: 18px;">APPLE ESPORTS</h2>
              <p style="margin:2px 0;">Gaming Cafe</p>
              <p style="margin:2px 0; font-size: 10px;">CREDIT SETTLEMENT RECEIPT</p>
            </div>
            <div class="border-b" style="margin-top: 5px;">
              <div class="flex"><span>Ref:</span><span>${idString}</span></div>
              ${fullBillObject.sessionStartTime ? `<div class="flex"><span>Played On:</span><span>${new Date(fullBillObject.sessionStartTime).toLocaleString()}</span></div>` : ''}
              <div class="flex"><span>Cleared On:</span><span>${new Date(fullBillObject.date || fullBillObject.createdAt).toLocaleString()}</span></div>
              <div class="flex"><span>Customer:</span><span>${fullBillObject.customer || fullBillObject.customerName || 'Walk-in'}</span></div>
              <div class="flex"><span>Operator:</span><span>${fullBillObject.operator || 'Admin'}</span></div>
            </div>
            <div class="border-b">
              <div class="flex font-bold" style="font-size: 14px; margin-top: 5px;">
                <span>AMOUNT PAID:</span>
                <span>${Number(fullBillObject.totalRevenue || 0).toFixed(2)}</span>
              </div>
              <div class="text-center" style="margin-top: 5px; font-size: 10px;">(Past Debt Cleared)</div>
            </div>
            <div class="text-center" style="margin-top: 10px;">
              <p style="margin:2px 0;">Thank you!</p>
            </div>
            <script>
              setTimeout(() => { 
                window.print(); 
                window.close(); 
              }, 500);
            </script>
          </body>
        </html>
      `;
      printWindow.document.write(html);
      printWindow.document.close();
      return;
    }

    const isGuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(idString);
    const endpoint = isGuid ? `/bills/${idString}` : `/bills/by-number/${idString}`;
    const res = await api.get(endpoint);
    if (res.data.success && res.data.data) {
      const billData = res.data.data;
      
      const printWindow = window.open('', '_blank', 'width=800,height=600');
      if (!printWindow) {
        alert('Please allow popups to print bills.');
        return;
      }

      const itemsHtml = billData.items.map(item => `
        <div class="item-row">
          <span class="item-name">${item.itemName}</span>
          <span class="item-qty">${item.quantity}</span>
          <span class="item-price">${item.totalPrice.toFixed(2)}</span>
        </div>
      `).join('');

      const html = `
        <!DOCTYPE html>
        <html>
          <head>
            <title>Receipt - ${billData.billNumber}</title>
            <style>
              body { 
                font-family: 'Courier New', Courier, monospace; 
                width: 80mm; 
                margin: 0 auto; 
                padding: 10px;
                color: black; 
                background: white; 
                font-size: 12px; 
              }
              .text-center { text-align: center; }
              .text-right { text-align: right; }
              .flex { display: flex; justify-content: space-between; }
              .border-b { border-bottom: 1px dashed black; padding-bottom: 5px; margin-bottom: 5px; }
              .border-t { border-top: 1px dashed black; padding-top: 5px; margin-top: 5px; }
              .font-bold { font-weight: bold; }
              .item-row { display: flex; justify-content: space-between; margin-bottom: 3px; }
              .item-name { flex: 1; padding-right: 5px; }
              .item-qty { width: 30px; text-align: center; }
              .item-price { width: 60px; text-align: right; }
              @media print {
                body { margin: 0; padding: 0; }
                @page { margin: 0; }
              }
            </style>
          </head>
          <body>
            <div class="text-center border-b">
              <h2 style="margin:0; font-size: 18px;">APPLE ESPORTS</h2>
              <p style="margin:2px 0;">Gaming Cafe</p>
              <p style="margin:2px 0; font-size: 10px;">Tax Invoice</p>
            </div>
            <div class="border-b" style="margin-top: 5px;">
              <div class="flex"><span>Bill No:</span><span>${billData.billNumber}</span></div>
              <div class="flex"><span>Date:</span><span>${new Date(billData.createdAt).toLocaleString()}</span></div>
              <div class="flex"><span>Customer:</span><span>${billData.customerName || 'Walk-in'}</span></div>
            </div>
            <div class="border-b">
              <div class="item-row font-bold">
                <span class="item-name">Item</span>
                <span class="item-qty">Qty</span>
                <span class="item-price">Total</span>
              </div>
              ${itemsHtml}
            </div>
            <div class="border-b">
              <div class="flex"><span>Subtotal:</span><span>${billData.subtotal.toFixed(2)}</span></div>
              ${billData.discountAmount > 0 ? `<div class="flex"><span>Discount:</span><span>-${billData.discountAmount.toFixed(2)}</span></div>` : ''}
              <div class="flex font-bold" style="font-size: 14px; margin-top: 5px;"><span>TOTAL:</span><span>${billData.totalAmount.toFixed(2)}</span></div>
            </div>
            <div class="text-center" style="margin-top: 10px;">
              <p style="margin:2px 0;">Thank you for playing!</p>
            </div>
            <script>
              setTimeout(() => { 
                window.print(); 
                window.close(); 
              }, 500);
            </script>
          </body>
        </html>
      `;

      printWindow.document.write(html);
      printWindow.document.close();
    }
  } catch (error) {
    console.error('Failed to print bill:', error);
    alert('Failed to print bill. See console for details.');
  }
};
