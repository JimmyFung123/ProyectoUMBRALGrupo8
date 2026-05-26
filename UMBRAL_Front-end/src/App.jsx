import { useState } from 'react'
import { useAuth } from './auth/AuthProvider'
import { MissionList } from './components/Missions/MissionList'
import { OperatorIdentityBar } from './components/OperatorIdentityBar'
import { SessionDashboard } from './components/Sessions/SessionDashboard'
import { SessionList } from './components/Sessions/SessionList'
import { UsersList } from './components/Users/UsersList'

const BASE_TABS = [
  { key: 'missions', label: '🗺️ Misiones', adminOnly: false },
  { key: 'sessions', label: '🎮 Sesiones', adminOnly: false },
  { key: 'users',    label: '👥 Personal', adminOnly: true  }, // HU-23
]

function App() {
  const { isAdmin } = useAuth()
  const [activeTab, setActiveTab] = useState('missions')
  const [selectedSessionId, setSelectedSessionId] = useState(null)

  // Filtra las pestañas según el rol — los operadores no ven "Personal".
  const visibleTabs = BASE_TABS.filter(t => !t.adminOnly || isAdmin)

  function handleTabChange(tab) {
    setActiveTab(tab)
    // Limpiar el detalle al cambiar de pestaña
    setSelectedSessionId(null)
  }

  return (
    <div style={{ textAlign: 'left' }}>
      {/* ── HU-22/23: identidad del operador (ahora con datos del JWT) ── */}
      <OperatorIdentityBar />

      {/* ── Barra de pestañas ── */}
      <nav style={{
        display: 'flex', gap: '0.25rem',
        padding: '0.5rem 1rem',
        borderBottom: '2px solid #ddd',
        background: '#f9f9f9',
      }}>
        {visibleTabs.map(tab => (
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
      {activeTab === 'users' && isAdmin && <UsersList />}
    </div>
  )
}

export default App
