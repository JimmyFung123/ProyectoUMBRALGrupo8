import { useMemo } from 'react';
import { motion } from 'framer-motion';

/**
 * HU-28 — pure-CSS confetti burst used when a team finishes the mission.
 *
 * No canvas library; we generate N coloured chips, give each one a
 * deterministic-random origin/rotation/duration, and let framer-motion
 * animate them falling. The component mounts on demand and unmounts when
 * the parent stops rendering it — there's no global state.
 */

const COLORS = ['#fbbf24', '#34d399', '#60a5fa', '#f472b6', '#a78bfa', '#f87171'];
const PIECE_COUNT = 48;

interface Props {
  /** Pause the animation after this many seconds. Defaults to 5. */
  durationSeconds?: number;
}

interface Piece {
  left: number;
  delay: number;
  duration: number;
  rotateStart: number;
  rotateEnd: number;
  color: string;
  size: number;
}

function generatePieces(): Piece[] {
  // Deterministic-ish via Math.random — confetti doesn't need stability across
  // renders because the component is only mounted once per celebration.
  return Array.from({ length: PIECE_COUNT }, () => ({
    left: Math.random() * 100,
    delay: Math.random() * 0.6,
    duration: 2.4 + Math.random() * 2.5,
    rotateStart: Math.random() * 360,
    rotateEnd: Math.random() * 720 - 360,
    color: COLORS[Math.floor(Math.random() * COLORS.length)],
    size: 6 + Math.random() * 8,
  }));
}

export function Confetti({ durationSeconds = 5 }: Props) {
  const pieces = useMemo(generatePieces, []);

  return (
    <div
      aria-hidden
      style={{
        position: 'fixed',
        inset: 0,
        pointerEvents: 'none',
        overflow: 'hidden',
        zIndex: 2000,
      }}
    >
      {pieces.map((p, i) => (
        <motion.span
          key={i}
          initial={{ y: -40, rotate: p.rotateStart, opacity: 1 }}
          animate={{ y: '110vh', rotate: p.rotateEnd, opacity: [1, 1, 0] }}
          transition={{
            duration: p.duration,
            delay: p.delay,
            ease: 'easeIn',
            repeat: durationSeconds > p.duration ? Math.floor(durationSeconds / p.duration) : 0,
          }}
          style={{
            position: 'absolute',
            top: 0,
            left: `${p.left}%`,
            width: p.size,
            height: p.size * 0.4,
            background: p.color,
            borderRadius: 2,
            display: 'block',
            boxShadow: `0 0 6px ${p.color}55`,
          }}
        />
      ))}
    </div>
  );
}
