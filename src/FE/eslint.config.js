const next = require('eslint-config-next');

module.exports = [
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
    overrides: [
      {
        files: [
          'src/FE/hooks/useThrottle.ts',
          'src/FE/components/Markdown/MermaidBlock.tsx',
        ],
        rules: {
          'react-hooks/purity': 'error',
        },
      },
      {
        files: ['src/FE/components/settings/tabs/ApiKeysTab.tsx'],
        rules: {
          'react-hooks/globals': 'error',
        },
      },
    ],
  },
];
