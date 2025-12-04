const next = require('eslint-config-next');

module.exports = [
  ...next,
  {
    rules: {
      'react-hooks/exhaustive-deps': 'off',
      'react-hooks/rules-of-hooks': 'off',
      'react-hooks/immutability': 'off',
      'react-hooks/set-state-in-effect': 'off',
      'react-hooks/static-components': 'off',
      'react-hooks/globals': 'off',
      'react-hooks/preserve-manual-memoization': 'off',
      'react-hooks/purity': 'off',
      'react-hooks/incompatible-library': 'off',
      '@next/next/no-img-element': 'off',
      'no-anonymous-default-export': 'off',
      'import/no-anonymous-default-export': 'off',
    },
  },
];
