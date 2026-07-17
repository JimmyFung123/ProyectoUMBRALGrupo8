import { useEffect, useState } from 'react'
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

// Separación funcional por rol (acordada con el equipo, RB-10):
// - Administrador: Misiones · Sesiones (solo consulta) · Estadísticas · Sincronización · Personal.
// - Operador: Sesiones (gestión completa) únicamente.
// Las pestañas se filtran con `adminOnly` / `operatorOnly`; una pestaña con
// AMBAS en true queda visible para los dos roles (Sesiones). El admin la ve en
// modo consulta (canManage=false) y el operador la gestiona.
const BASE_TABS = [
  { key: 'missions',   label: 'Misiones',       icon: '🗺️', adminOnly: true,  operatorOnly: false },
  { key: 'sessions',   label: 'Sesiones',       icon: '🎮', adminOnly: true,  operatorOnly: true  },
  { key: 'statistics', label: 'Estadísticas',   icon: '📊', adminOnly: true,  operatorOnly: false }, // HU-25
  { key: 'sync',       label: 'Sincronización', icon: '🔄', adminOnly: true,  operatorOnly: false }, // HU-27
  { key: 'users',      label: 'Personal',       icon: '👥', adminOnly: true,  operatorOnly: false }, // HU-23
]

function App() {
  const { isAdmin } = useAuth()

  // Calcula la lista visible según el rol y elige la pestaña inicial coherente
  // con esa lista. Si el rol cambia (ej. tras un refresh con token nuevo) y la
  // pestaña activa ya no aplica, el efecto la reajusta automáticamente.
  const visibleTabs = BASE_TABS.filter(t =>
    isAdmin ? t.adminOnly : t.operatorOnly,
  )
  const defaultTab = visibleTabs[0]?.key ?? 'missions'

  const [activeTab, setActiveTab] = useState(defaultTab)
  const [selectedSessionId, setSelectedSessionId] = useState(null)
  // HU-26: cuando es no-null, se renderiza la pantalla completa de auditoría
  // técnica en lugar del dashboard. Vive en el mismo estado para mantener la
  // misma sesión seleccionada al volver con "Volver al dashboard".
  const [commandAuditSessionId, setCommandAuditSessionId] = useState(null)

  useEffect(() => {
    if (!visibleTabs.some(t => t.key === activeTab)) {
      setActiveTab(defaultTab)
      setSelectedSessionId(null)
      setCommandAuditSessionId(null)
    }
  }, [activeTab, defaultTab, visibleTabs])

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
        {activeTab === 'missions' && isAdmin && <MissionList />}
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
                  canManage={!isAdmin}
                  onBack={() => setSelectedSessionId(null)}
                  onOpenCommandAudit={() => setCommandAuditSessionId(selectedSessionId)}
                />
              )
              : (
                <SessionList canManage={!isAdmin} onViewDetail={setSelectedSessionId} />
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
