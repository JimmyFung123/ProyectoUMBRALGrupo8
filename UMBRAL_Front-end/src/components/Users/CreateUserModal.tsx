import { useState } from 'react';
import { userService } from '../../services/userService';
import type { UserRole } from '../../types/user';
import {
  Alert,
  Button,
  FormField,
  Modal,
  Select,
  Stack,
  TextInput,
} from '../ui';

interface Props {
  onClose: () => void;
  onCreated: () => void | Promise<void>;
}

interface BackendError { code?: string; message?: string }

const MIN_PASSWORD_LENGTH = 8;

/**
 * HU-23 Criterio 1: registra un nuevo administrador u operador.
 *
 * Fix del bug reportado por el equipo: antes el botón "Crear usuario"
 * permanecía deshabilitado en silencio cuando la contraseña tenía < 8
 * caracteres, sin pista clara para el usuario. Ahora el botón siempre
 * intenta enviar y, si la validación local falla, muestra un mensaje
 * específico arriba del formulario. La regla del backend (email único,
 * 409 → "Este correo ya está en uso") se sigue respetando.
 */
export function CreateUserModal({ onClose, onCreated }: Props) {
  const [email, setEmail] = useState('');
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [temporaryPassword, setTemporaryPassword] = useState('');
  const [role, setRole] = useState<UserRole>('Operator');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (submitting) return;

    // Validación local con mensajes específicos en vez de deshabilitar el
    // botón en silencio. Mucho más fácil de entender para el operador.
    const trimmedEmail = email.trim();
    const trimmedFirst = firstName.trim();
    const trimmedLast = lastName.trim();

    if (!trimmedEmail || !trimmedFirst || !trimmedLast || !temporaryPassword) {
      setError('Todos los campos son obligatorios.');
      return;
    }
    if (temporaryPassword.length < MIN_PASSWORD_LENGTH) {
      setError(`La contraseña temporal debe tener al menos ${MIN_PASSWORD_LENGTH} caracteres.`);
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      await userService.create({
        email: trimmedEmail.toLowerCase(),
        firstName: trimmedFirst,
        lastName: trimmedLast,
        temporaryPassword,
        role,
      });
      await onCreated();
    } catch (err) {
      const be = err as BackendError | undefined;
      setError(be?.message ?? 'No se pudo crear el usuario.');
      setSubmitting(false);
    }
  }

  return (
    <Modal
      open
      onClose={submitting ? () => undefined : onClose}
      title="➕ Nuevo usuario"
      description="Se creará una cuenta en Keycloak. El usuario podrá ingresar con el correo y la contraseña temporal."
      size="md"
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={submitting}>
            Cancelar
          </Button>
          <Button type="submit" form="create-user-form" disabled={submitting}>
            {submitting ? 'Creando…' : 'Crear usuario'}
          </Button>
        </>
      }
    >
      <form id="create-user-form" onSubmit={handleSubmit}>
        <Stack gap={3}>
          {error && <Alert tone="danger" onDismiss={() => setError(null)}>{error}</Alert>}

          <FormField label="Correo electrónico" htmlFor="create-email" required>
            <TextInput
              id="create-email"
              type="email"
              value={email}
              onChange={e => setEmail(e.target.value)}
              placeholder="usuario@umbral.local"
              autoFocus
              required
            />
          </FormField>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <FormField label="Nombre" htmlFor="create-first" required>
              <TextInput
                id="create-first"
                value={firstName}
                onChange={e => setFirstName(e.target.value)}
                required
              />
            </FormField>
            <FormField label="Apellido" htmlFor="create-last" required>
              <TextInput
                id="create-last"
                value={lastName}
                onChange={e => setLastName(e.target.value)}
                required
              />
            </FormField>
          </div>

          <FormField
            label="Contraseña temporal"
            htmlFor="create-pwd"
            required
            hint={`Mínimo ${MIN_PASSWORD_LENGTH} caracteres. El usuario podrá ingresar directamente con esta clave.`}
          >
            <TextInput
              id="create-pwd"
              type="text"
              value={temporaryPassword}
              onChange={e => setTemporaryPassword(e.target.value)}
              placeholder="ej. Umbral2026!"
              className="font-mono"
            />
          </FormField>

          <FormField label="Rol" htmlFor="create-role" required>
            <Select
              id="create-role"
              value={role}
              onChange={e => setRole(e.target.value as UserRole)}
            >
              <option value="Operator">Operador — gestiona sesiones en vivo</option>
              <option value="Admin">Administrador — misiones, estadísticas, sincronización y personal</option>
            </Select>
          </FormField>
        </Stack>
      </form>
    </Modal>
  );
}
