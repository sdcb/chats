export function mergeConfigs(obj1: any, obj2: any) {
  const config = Object.keys(obj1 || {}).reduce((result: any, key) => {
    result[key] = obj2[key] === null ? '' : obj2[key];
    return result;
  }, {});
  return JSON.stringify(config, null, 2);
}

