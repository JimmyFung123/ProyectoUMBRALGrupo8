import { useState } from 'react'
import { MissionList } from './components/Missions/MissionList'
import { OperatorIdentityBar } from './components/OperatorIdentityBar'
import { SessionDashboard } from './components/Sessions/SessionDashboard'
import { SessionList } from './components/Sessions/SessionList'

const TABS = [
  { key: 'missions', label: '🗺️ Misiones' },
  { key: 'sessions', label: '🎮 Sesiones' },
]

function App() {
  const [activeTab, setActiveTab] = useState('missions')
  const [selectedSessionId, setSelectedSessionId] = useState(null)

  function handleTabChange(tab) {
    setActiveTab(tab)
    // Limpiar el detalle al cambiar de pestaña
    setSelectedSessionId(null)
  }

  return (
    <div style={{ textAlign: 'left' }}>
      {/* ── HU-22: identidad del operador para el audit log ── */}
      <OperatorIdentityBar />

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
            onClick={() => handleTabChange(tab.key)}
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
      {activeTab === 'sessions' && (
        selectedSessionId
          ? (
            <SessionDashboard
              sessionId={selectedSessionId}
              onBack={() => setSelectedSessionId(null)}
            />
          )
          : (
            <SessionList onViewDetail={setSelectedSessionId} />
          )
      )}
    </div>
  )
}

export default App
