import { ComponentType, FC, ReactNode, useEffect, useState } from 'react';

interface LoadComponentOnceOptions<TProps extends object> {
  cacheKey: string;
  loader: () => Promise<ComponentType<TProps>>;
  renderFallback?: (props: TProps) => ReactNode;
}

interface CachedComponentEntry<TProps extends object> {
  component: ComponentType<TProps> | null;
  promise: Promise<ComponentType<TProps>> | null;
}

const componentCache = new Map<string, CachedComponentEntry<any>>();

const getCachedEntry = <TProps extends object>(
  cacheKey: string,
): CachedComponentEntry<TProps> => {
  const existing = componentCache.get(cacheKey) as
    | CachedComponentEntry<TProps>
    | undefined;

  if (existing) {
    return existing;
  }

  const initialEntry: CachedComponentEntry<TProps> = {
    component: null,
    promise: null,
  };
  componentCache.set(cacheKey, initialEntry);
  return initialEntry;
};

const loadCachedComponent = <TProps extends object>(
  cacheKey: string,
  loader: () => Promise<ComponentType<TProps>>,
) => {
  const entry = getCachedEntry<TProps>(cacheKey);

  if (entry.component) {
    return Promise.resolve(entry.component);
  }

  if (entry.promise) {
    return entry.promise;
  }

  entry.promise = loader()
    .then((loadedComponent) => {
      entry.component = loadedComponent;
      return loadedComponent;
    })
    .catch((error) => {
      componentCache.delete(cacheKey);
      throw error;
    })
    .finally(() => {
      const latestEntry = componentCache.get(cacheKey) as
        | CachedComponentEntry<TProps>
        | undefined;
      if (latestEntry) {
        latestEntry.promise = null;
      }
    });

  return entry.promise;
};

const getLoadedComponent = <TProps extends object>(cacheKey: string) =>
  (componentCache.get(cacheKey)?.component ?? null) as ComponentType<TProps> | null;

export const loadComponentOnce = <TProps extends object>({
  cacheKey,
  loader,
  renderFallback,
}: LoadComponentOnceOptions<TProps>): FC<TProps> => {
  const LoadOnceComponent: FC<TProps> = (props) => {
    const [component, setComponent] = useState<ComponentType<TProps> | null>(
      () => getLoadedComponent<TProps>(cacheKey),
    );

    useEffect(() => {
      const loadedComponent = getLoadedComponent<TProps>(cacheKey);
      if (loadedComponent) {
        if (component !== loadedComponent) {
          setComponent(() => loadedComponent);
        }
        return;
      }

      let mounted = true;

      void loadCachedComponent(cacheKey, loader)
        .then((resolvedComponent) => {
          if (mounted) {
            setComponent(() => resolvedComponent);
          }
        })
        .catch((error) => {
          console.error(`Failed to lazy-load component "${cacheKey}":`, error);
        });

      return () => {
        mounted = false;
      };
    }, [cacheKey, component, loader]);

    const ResolvedComponent = component ?? getLoadedComponent<TProps>(cacheKey);

    if (ResolvedComponent) {
      return <ResolvedComponent {...props} />;
    }

    return <>{renderFallback?.(props) ?? null}</>;
  };

  LoadOnceComponent.displayName = `LoadOnce(${cacheKey})`;

  return LoadOnceComponent;
};
