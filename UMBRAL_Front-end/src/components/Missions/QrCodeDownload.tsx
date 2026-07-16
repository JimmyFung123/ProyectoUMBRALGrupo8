import { useRef } from 'react';
import { QRCodeCanvas } from 'qrcode.react';

// Tamaño de exportación (px). El QR visible se mantiene chico por layout, pero el
// PNG se exporta grande para que imprima nítido y los lectores lo enfoquen bien.
const QR_EXPORT_SIZE = 2048;

interface Props {
  /** Valor codificado en el QR (el identificador de la etapa de tesoro). */
  value: string;
  /** Tamaño del QR visible en px. La descarga siempre se exporta en alta resolución. */
  size?: number;
}

/**
 * QR imprimible reutilizable: muestra el código y permite descargarlo.
 * Existe para poder ofrecer la descarga tanto al configurar la etapa como al
 * consultarla, sin duplicar el canvas oculto de alta resolución.
 */
export function QrCodeDownload({ value, size = 140 }: Props) {
  // Canvas oculto en alta resolución, usado solo para exportar el QR a imprimir.
  const exportRef = useRef<HTMLDivElement>(null);

  function handleDownload() {
    // Exportamos desde el canvas oculto de alta resolución, no del visible,
    // para que el PNG descargado no salga pixelado al imprimirlo.
    const canvas = exportRef.current?.querySelector('canvas') as HTMLCanvasElement | null;
    if (!canvas) return;
    const link = document.createElement('a');
    link.href = canvas.toDataURL('image/png');
    link.download = `qr-${value.slice(0, 8)}.png`;
    link.click();
  }

  return (
    <div className="flex flex-col items-center gap-2 bg-white rounded-xl border border-slate-200 p-3">
      <QRCodeCanvas value={value} size={size} bgColor="#ffffff" fgColor="#1e293b" level="H" />

      {/* Canvas oculto en alta resolución: solo se usa para exportar. Se posiciona
          fuera de pantalla (no display:none, que impediría pintar el canvas).
          marginSize agrega el "quiet zone" que los lectores necesitan para enfocar. */}
      <div
        ref={exportRef}
        aria-hidden="true"
        style={{ position: 'absolute', left: '-99999px', top: 0, pointerEvents: 'none' }}
      >
        <QRCodeCanvas value={value} size={QR_EXPORT_SIZE} bgColor="#ffffff" fgColor="#1e293b" level="H" marginSize={4} />
      </div>

      <p className="text-[9px] font-mono text-slate-400 break-all text-center leading-relaxed">
        {value}
      </p>

      <button
        type="button"
        onClick={handleDownload}
        className="w-full flex items-center justify-center gap-1.5 text-xs bg-slate-800 hover:bg-slate-700 text-white py-1.5 rounded-lg transition-colors cursor-pointer"
      >
        ⬇ Descargar QR para imprimir
      </button>
    </div>
  );
}
