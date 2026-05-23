import { useState } from 'react'
import { MissionList } from './components/Missions/MissionList'
import { SessionList } from './components/Sessions/SessionList'

const TABS = [
  { key: 'missions', label: '🗺️ Misiones' },
  { key: 'sessions', label: '🎮 Sesiones' },
]

function App() {
  const [activeTab, setActiveTab] = useState('missions')

  return (
    <div style={{ textAlign: 'left' }}>
      {/* ── Barra de pestañas ── */}
      <nav style={{
        display: 'flex', gap: '0.25rem',
        padding: '0.5rem 1rem',
        borderBottom: '2px solid #ddd',
        background: '#f9f9f9',
      }}>
        {TABS.map(tab => (
          <button
            key={tab.key}
            onClick={() => setActiveTab(tab.key)}
            style={{
              padding: '0.4rem 1rem',
              border: '1px solid #ccc',
              borderRadius: '4px 4px 0 0',
              background: activeTab === tab.key ? '#fff' : '#eee',
              fontWeight: activeTab === tab.key ? 'bold' : 'normal',
              cursor: 'pointer',
              borderBottom: activeTab === tab.key ? '2px solid #fff' : '1px solid #ccc',
              marginBottom: '-2px',
            }}
          >
            {tab.label}
          </button>
        ))}
      </nav>

      {/* ── Contenido ── */}
      {activeTab === 'missions' && <MissionList />}
      {activeTab === 'sessions' && <SessionList />}
    </div>
  )
}

export default App
