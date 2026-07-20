import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import tseslint from 'typescript-eslint'
import { defineConfig, globalIgnores } from 'eslint/config'

export default defineConfig([
  // Build outputs and machine-generated code are never hand-edited (the orval
  // output carries a deliberate @ts-nocheck header) — linting them is noise.
  globalIgnores(['dist', 'coverage', 'src/api/generated']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      globals: globals.browser,
    },
  },
  {
    // Test harness and test files are not fast-refresh surfaces: they export
    // providers, helpers and re-exports by design.
    files: ['src/test/**/*.{ts,tsx}', 'src/**/*.{test,spec}.{ts,tsx}'],
    rules: {
      'react-refresh/only-export-components': 'off',
    },
  },
  {
    // Chromatic single-source: raw hex values live ONLY in src/app/palette.ts —
    // everything else derives from it (semantic palettes) or reads `token.*`
    // in createStyles. Tests may pin expected hexes (they verify derivation).
    files: ['src/**/*.{ts,tsx}'],
    ignores: ['src/app/palette.ts', 'src/test/**', 'src/**/*.{test,spec}.{ts,tsx}'],
    rules: {
      'no-restricted-syntax': [
        'error',
        {
          selector: 'Literal[value=/#[0-9a-fA-F]{3,8}\\b/]',
          message: 'Raw hex colours live only in src/app/palette.ts — derive from the palette or use token.*.',
        },
        {
          selector: 'TemplateElement[value.raw=/#[0-9a-fA-F]{3,8}\\b/]',
          message: 'Raw hex colours live only in src/app/palette.ts — derive from the palette or use token.*.',
        },
      ],
    },
  },
])
