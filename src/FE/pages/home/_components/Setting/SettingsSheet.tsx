import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet';

type Props = {
  isOpen: boolean;
};
const SettingsSheet = (props: Props) => {
  const { isOpen } = props;
  return (
    <Sheet open={isOpen}>
      <SheetContent className="w-full">
        <SheetHeader>
          <SheetTitle>Are you absolutely sure?</SheetTitle>
          <SheetDescription>
            This action cannot be undone. This will permanently delete your
            account and remove your data from our servers.
          </SheetDescription>
        </SheetHeader>
      </SheetContent>
    </Sheet>
  );
};

export default SettingsSheet;
