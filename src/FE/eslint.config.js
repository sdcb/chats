const next = require('eslint-config-next');

module.exports = [
  {
    ignores: ['.next/**', 'out/**', 'out-local/**', 'node_modules/**'],
  },
  ...next,
  {
    linterOptions: {
      reportUnusedDisableDirectives: 'off',
    },
    rules: {
      'react-hooks/exhaustive-deps': 'off',
      'react-hooks/rules-of-hooks': 'error',
      'react-hooks/immutability': 'off',
      'react-hooks/set-state-in-effect': 'off',
      'react-hooks/static-components': 'error',
      'react-hooks/globals': 'off',
      'react-hooks/preserve-manual-memoization': 'error',
      'react-hooks/purity': 'off',
      'react-hooks/incompatible-library': 'off',
      '@next/next/no-img-element': 'off',
      'import/no-anonymous-default-export': 'error',
    },
  },
  {
    files: ['hooks/useThrottle.ts', 'components/Markdown/MermaidBlock.tsx'],
    rules: {
      'react-hooks/purity': 'error',
    },
  },
  {
    files: ['components/settings/tabs/ApiKeysTab.tsx'],
    rules: {
      'react-hooks/globals': 'error',
    },
  },
];
