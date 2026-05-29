import { useState } from 'react'
import { useAuth } from './auth/AuthProvider'
import { MissionList } from './components/Missions/MissionList'
import { OperatorIdentityBar } from './components/OperatorIdentityBar'
import { SessionCommandAuditScreen } from './components/Sessions/SessionCommandAuditScreen'
import { SessionDashboard } from './components/Sessions/SessionDashboard'
import { SessionList } from './components/Sessions/SessionList'
import { StatisticsDashboard } from './components/Statistics/StatisticsDashboard'
import { SyncHealthDashboard } from './components/SyncHealth/SyncHealthDashboard'
import { UsersList } from './components/Users/UsersList'

const BASE_TABS = [
  { key: 'missions',   label: '🗺️ Misiones',     adminOnly: false },
  { key: 'sessions',   label: '🎮 Sesiones',     adminOnly: false },
  { key: 'statistics', label: '📊 Estadísticas', adminOnly: true  }, // HU-25
  { key: 'sync',       label: '🔄 Sincronización', adminOnly: true }, // HU-27
  { key: 'users',      label: '👥 Personal',     adminOnly: true  }, // HU-23
]

function App() {
  const { isAdmin } = useAuth()
  const [activeTab, setActiveTab] = useState('missions')
  const [selectedSessionId, setSelectedSessionId] = useState(null)
  // HU-26: cuando es no-null, se renderiza la pantalla completa de auditoría
  // técnica en lugar del dashboard. Vive en el mismo estado para mantener la
  // misma sesión seleccionada al volver con "Volver al dashboard".
  const [commandAuditSessionId, setCommandAuditSessionId] = useState(null)

  // Filtra las pestañas según el rol — los operadores no ven "Personal".
  const visibleTabs = BASE_TABS.filter(t => !t.adminOnly || isAdmin)

  function handleTabChange(tab) {
    setActiveTab(tab)
    // Limpiar el detalle al cambiar de pestaña
    setSelectedSessionId(null)
    setCommandAuditSessionId(null)
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
        commandAuditSessionId
          ? (
            // HU-26: pantalla técnica completa, separada del dashboard.
            <SessionCommandAuditScreen
              sessionId={commandAuditSessionId}
              onBack={() => setCommandAuditSessionId(null)}
            />
          )
          : selectedSessionId
            ? (
              <SessionDashboard
                sessionId={selectedSessionId}
                onBack={() => setSelectedSessionId(null)}
                onOpenCommandAudit={() => setCommandAuditSessionId(selectedSessionId)}
              />
            )
            : (
              <SessionList onViewDetail={setSelectedSessionId} />
            )
      )}
      {activeTab === 'statistics' && isAdmin && <StatisticsDashboard />}
      {activeTab === 'sync' && isAdmin && <SyncHealthDashboard />}
      {activeTab === 'users' && isAdmin && <UsersList />}
    </div>
  )
}

export default App
