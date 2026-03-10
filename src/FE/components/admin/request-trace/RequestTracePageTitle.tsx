import { useState } from 'react';

import { IconDoorIn, IconDoorOut } from '@/components/Icons';
import Tips from '@/components/Tips/Tips';
import { Button } from '@/components/ui/button';
import useTranslation from '@/hooks/useTranslation';

import RequestTraceConfigDialog from './RequestTraceConfigDialog';

export default function RequestTracePageTitle() {
  const { t } = useTranslation();
  const [inboundOpen, setInboundOpen] = useState(false);
  const [outboundOpen, setOutboundOpen] = useState(false);

  return (
    <>
      {t('Request Trace')}
      <Tips
        trigger={
          <Button
            variant="outline"
            size="sm"
            className="h-8 w-8 p-0 ml-3"
            onClick={() => setInboundOpen(true)}
          >
            <IconDoorIn size={18} />
          </Button>
        }
        side="bottom"
        content={t('Configure inbound tracing')}
      />
      <Tips
        trigger={
          <Button
            variant="outline"
            size="sm"
            className="h-8 w-8 p-0 ml-1"
            onClick={() => setOutboundOpen(true)}
          >
            <IconDoorOut size={18} />
          </Button>
        }
        side="bottom"
        content={t('Configure outbound tracing')}
      />
      <RequestTraceConfigDialog
        direction="inbound"
        open={inboundOpen}
        onOpenChange={setInboundOpen}
      />
      <RequestTraceConfigDialog
        direction="outbound"
        open={outboundOpen}
        onOpenChange={setOutboundOpen}
      />
    </>
  );
}
