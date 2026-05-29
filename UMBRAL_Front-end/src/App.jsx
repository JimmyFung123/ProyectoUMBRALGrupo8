import { useState } from 'react'
import { useAuth } from './auth/AuthProvider'
import { MissionList } from './components/Missions/MissionList'
import { OperatorIdentityBar } from './components/OperatorIdentityBar'
import { SessionCommandAuditScreen } from './components/Sessions/SessionCommandAuditScreen'
import { SessionDashboard } from './components/Sessions/SessionDashboard'
import { SessionList } from './components/Sessions/SessionList'
import { StatisticsDashboard } from './components/Statistics/StatisticsDashboard'
import { SyncHealthDashboard } from './components/SyncHealth/SyncHealthDashboard'
import { Tabs } from './components/ui'
import { UsersList } from './components/Users/UsersList'

const BASE_TABS = [
  { key: 'missions',   label: 'Misiones',       icon: '🗺️', adminOnly: false },
  { key: 'sessions',   label: 'Sesiones',       icon: '🎮', adminOnly: false },
  { key: 'statistics', label: 'Estadísticas',   icon: '📊', adminOnly: true  }, // HU-25
  { key: 'sync',       label: 'Sincronización', icon: '🔄', adminOnly: true  }, // HU-27
  { key: 'users',      label: 'Personal',       icon: '👥', adminOnly: true  }, // HU-23
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
    <div className="min-h-screen bg-surface-base text-ink">
      <OperatorIdentityBar />

      <Tabs
        tabs={visibleTabs}
        active={activeTab}
        onChange={handleTabChange}
      />

      <main className="max-w-7xl mx-auto px-4 md:px-6 py-6">
        {activeTab === 'missions' && <MissionList />}
        {activeTab === 'sessions' && (
          commandAuditSessionId
            ? (
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
      </main>
    </div>
  )
}

export default App
