import { useEffect, useState } from 'react';

import { getApiUrl } from '@/utils/common';

import { Button } from '@/components/ui/button';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';

import { getCsrfToken } from '@/apis/clientApis';

const KeyCloakLogin = (props: { loading?: boolean }) => {
  const { loading } = props;
  const [csrfToken, setCsrfToken] = useState<string>('');
  useEffect(() => {
    getCsrfToken().then((data) => {
      setCsrfToken(data?.csrfToken);
    });
  }, []);

  return (
    <form action={`${getApiUrl()}/api/auth/signin/keycloak`} method="POST">
      <input type="hidden" name="csrfToken" value={csrfToken} />
      <input
        type="hidden"
        name="callbackUrl"
        value={`${location.origin}/authorizing?provider=Keycloak`}
      />
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              className="p-0 w-8 h-8"
              disabled={loading}
              variant="link"
              type="submit"
            >
              <img
                src="/logos/keycloak.svg"
                alt="Keycloak"
                width={32}
                height={32}
                className="h-8 w-8 rounded-md"
              />
            </Button>
          </TooltipTrigger>
          <TooltipContent>Keycloak</TooltipContent>
        </Tooltip>
      </TooltipProvider>
    </form>
  );
};
export default KeyCloakLogin;
