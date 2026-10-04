import js from '@eslint/js';
import tseslint from 'typescript-eslint';
export default tseslint.config({ ignores: ['dist/**', 'node_modules/**'] }, js.configs.recommended, ...tseslint.configs.recommended, { rules: { 'no-irregular-whitespace': ['error', { skipTemplates: true }] } });
