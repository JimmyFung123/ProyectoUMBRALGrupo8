import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.jsx'
import { AuthProvider } from './auth/AuthProvider'

// HU-23: AuthProvider envuelve la app entera. Mientras Keycloak no resuelva
// init({ onLoad: 'login-required' }), AuthProvider muestra un splash; luego
// o bien redirige al login del realm o ya entrega los claims del JWT.
createRoot(document.getElementById('root')).render(
  <StrictMode>
    <AuthProvider>
      <App />
    </AuthProvider>
  </StrictMode>,
)
