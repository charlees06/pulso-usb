# Pulso · USB Control

Aplicación para Windows con ajustes de frecuencia USB, probador de controles y una interfaz en español, inglés, portugués brasileño, chino simplificado y ucraniano.

## Instalar

Descarga [Pulso-Setup-2.4.0-x64.exe]([https://jrsoftware.org/isdl.php](https://github.com/charlees06/pulso-usb/releases/tag/v2.4.0) en **Releases** y sigue el asistente. Instala Pulso para tu usuario y crea accesos en el escritorio y el menú Inicio. Incluye el instalador oficial de .NET Framework 4.8 y HIDUSBF NoPatch x64.

Para la barra de tareas, abre **Pulso → Guía rápida → Anclar a la barra de tareas**. En versiones compatibles se muestra la confirmación de Windows. En las demás, la app indica cómo hacerlo con el menú del icono. El instalador no fuerza el anclaje.

También puedes usar [Pulso-Completo-Windows-x64.zip](https://github.com/charlees06/pulso-usb/releases/tag/v2.4.0) : extrae todo su contenido y abre el `Pulso.exe` de la carpeta principal.

## Funciones

- Identificación de dispositivos de entrada USB sin una lista cerrada de marcas.
- Revisión de los cambios antes de aplicarlos y conservación de ajustes originales.
- Preparación guiada del componente de ajustes USB cuando falta.
- Probador con botones, palancas, cruceta y gatillos, mediante HID y XInput.
- Cambio de idioma desde la interfaz.

El probador funciona sin HIDUSBF. Para cambiar frecuencias se necesita ese controlador y permisos de administrador. La instalación del componente no aplica una frecuencia automáticamente.

## Compatibilidad

Windows 10/11 de 64 bits, con procesador Intel o AMD, y .NET Framework 4.8. No se admiten Windows ARM ni sistemas de 32 bits.

La frecuencia solicitada depende de lo que admita el dispositivo: no elimina toda la latencia ni garantiza una frecuencia efectiva. El paquete usa la edición NoPatch de HIDUSBF; los dispositivos Low Speed no admiten aumentos con esa edición. El probador no mide latencia.

Pulso y su instalador todavía no tienen firma digital propia. Los componentes de Microsoft y HIDUSBF conservan sus firmas y archivos originales.

## Compilar y probar

Desde PowerShell en Windows con .NET Framework 4.8:

```powershell
.\Compilar.ps1
.\Probar.ps1
```

La app se genera en `build/app/Pulso-Idiomas.exe`. Las pruebas leen los dispositivos presentes, validan idiomas e interfaz y usan configuraciones simuladas para comprobar cambios y recuperación. No instalan controladores ni alteran los ajustes USB. Sus informes permanecen en `build/` y no se publican.

Para generar el instalador y el ZIP completo, instala [Inno Setup 6.7.3](https://jrsoftware.org/isdl.php) y ejecuta:

```powershell
.\Crear-Instalador.ps1 -CompilerPath 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
```

El script descarga el instalador oficial de .NET si falta, verifica su SHA-256 y las firmas de los componentes, ejecuta las pruebas y escribe los paquetes y `SHA256SUMS.txt` en `dist/`.

## Organización

| Ruta | Contenido |
| --- | --- |
| `src/` | Aplicación WPF, lectura USB/HID, traducciones y pruebas |
| `src/DriverBundle/` | Binarios oficiales de HIDUSBF incluidos sin modificar |
| `installer/` | Instalador y traducción complementaria |
| `Compilar.ps1` | Compilación de la aplicación |
| `Probar.ps1` | Pruebas locales sin cambios de hardware |
| `Crear-Instalador.ps1` | Creación de los paquetes de distribución |

## Recuperación y desinstalación

Las copias originales de los dispositivos se guardan en `Aplicacion/copias`, junto al ejecutable instalado. Conserva esa carpeta al mover o actualizar Pulso. El desinstalador conserva los archivos de recuperación generados y la preferencia de idioma; no desinstala el controlador compartido de HIDUSBF ni .NET.

Para quitar HIDUSBF, primero retira sus filtros de los dispositivos y sigue la [guía oficial](https://github.com/LordOfMice/hidusbf). El repositorio y los paquetes de distribución no contienen las copias personales del equipo de desarrollo.

## Componentes externos

La procedencia, revisión y huellas de HIDUSBF y .NET están en [TERCEROS.txt](src/TERCEROS.txt). Las condiciones de esos componentes se mantienen separadas de las del código de Pulso. La publicación de este repositorio no añade por sí sola una licencia de reutilización al código de Pulso.

## Estado de validación

La interfaz, los cinco idiomas, el probador y la apertura sin HIDUSBF se comprobaron en Windows 11. La instalación de HIDUSBF desde cero en un equipo diferente sigue pendiente de validación real. Ninguna prueba automática cambia las medidas de seguridad de Windows.
