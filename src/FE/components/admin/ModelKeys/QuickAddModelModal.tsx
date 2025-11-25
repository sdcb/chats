import React, { useEffect, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { IconInfo } from '@/components/Icons';
import Spinner from '@/components/Spinner/Spinner';
import Tips from '@/components/Tips/Tips';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';

import {
  getModelKeyPossibleModels,
  postModels,
  postModelValidate,
} from '@/apis/adminApis';
import { UpdateModelDto, AdminModelDto } from '@/types/adminApis';
import {
  ApiType,
  getDefaultConfigByApiType,
} from '@/constants/modelDefaults';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

interface IProps {
  modelKeyId: number;
  modelProverId: number;
  isOpen: boolean;
  onClose: () => void;
  onSuccessful: () => void;
  onOpenEditModel?: (deploymentName: string, apiType: number) => void; // 新增：打开编辑模型对话框的回调
}

export interface PossibleModel {
  deploymentName: string;
  existingModel: AdminModelDto | null;
  validating: boolean;
  validateMessage: string | null;
  validateSuccess: boolean; // 新增：验证是否成功
  adding: boolean;
  apiType: number; // 用户选择的API类型: 0=ChatCompletion, 1=Response, 2=ImageGeneration
}

const QuickAddModelModal = (props: IProps) => {
  const { t } = useTranslation();
  const { modelKeyId, isOpen, onClose, onOpenEditModel } = props;
  const [models, setModels] = useState<PossibleModel[]>([]);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [errorDetail, setErrorDetail] = React.useState<string | null>(null);
  const [searchQuery, setSearchQuery] = React.useState<string>('');

  // 加载可用模型列表
  const loadModels = () => {
    setLoading(true);
    setError(null);
    setErrorDetail(null);
    
    getModelKeyPossibleModels(modelKeyId).then((data) => {
      const processedModels = data.map((x) => ({
        deploymentName: x.deploymentName,
        existingModel: x.existingModel,
        validating: false,
        validateSuccess: false,
        adding: false,
        validateMessage: null,
        apiType: x.existingModel?.apiType ?? 0, // 如果已存在，使用其 API 类型，否则默认为 ChatCompletion
      }));
      
      // 将已存在的模型排在最后
      const sortedModels = processedModels.sort((a, b) => {
        if (a.existingModel && !b.existingModel) return 1;
        if (!a.existingModel && b.existingModel) return -1;
        return 0;
      });
      
      setModels(sortedModels);
      setLoading(false);
    }).catch((error) => {
      console.error('Failed to fetch models:', error);
      setError(t('This model provider does not support quick model creation'));
      // 捕获后端返回的详细错误信息
      if (typeof error === 'string') {
        setErrorDetail(error);
      } else if (error && typeof error === 'object' && 'message' in error) {
        setErrorDetail(error.message);
      }
      setLoading(false);
    });
  };

  useEffect(() => {
    if (isOpen) {
      setModels([]);
      setSearchQuery(''); // 重置搜索
      setErrorDetail(null); // 重置错误详情
      loadModels();
    }
  }, [isOpen]);

  function handleAdd(index: number) {
    const model = models[index];
    const modelList = [...models];
    model.adding = true;
    setModels([...modelList.map((x, i) => (i === index ? model : x))]);
    
    // 根据用户选择的 API 类型使用相应的默认配置
    const defaultConfig = getDefaultConfigByApiType(model.apiType as ApiType);
    
    // 使用普通创建接口，传入完整配置
    const dto: UpdateModelDto = {
      name: model.deploymentName,
      deploymentName: model.deploymentName,
      modelKeyId: modelKeyId,
      enabled: true,
      inputTokenPrice1M: 0,
      outputTokenPrice1M: 0,
      supportsVisionLink: false,
      ...defaultConfig,
    } as UpdateModelDto;
    
    postModels(dto).then(() => {
      toast.success(t('Added successfully'));
      // 通知父组件刷新（但不关闭对话框）
      props.onSuccessful();
      
      // 重新加载模型列表以更新状态（显示为已存在）
      loadModels();
    }).catch((error) => {
      console.error('Failed to add model:', error);
      toast.error(t('Failed to add model'));
      model.adding = false;
      setModels([...modelList.map((x, i) => (i === index ? model : x))]);
    });
  }

  function handleValidate(index: number) {
    const model = models[index];
    const modelList = [...models];
    model.validating = true;
    setModels([...modelList.map((x, i) => (i === index ? model : x))]);
    
    // 根据用户选择的 API 类型使用相应的默认配置
    const defaultConfig = getDefaultConfigByApiType(model.apiType as ApiType);
    
    // 构造完整的配置对象进行验证
    const params: UpdateModelDto = {
      name: model.deploymentName,
      deploymentName: model.deploymentName,
      modelKeyId: modelKeyId,
      enabled: true,
      inputTokenPrice1M: 0,
      outputTokenPrice1M: 0,
      supportsVisionLink: false,
      ...defaultConfig,
    } as UpdateModelDto;
    
    postModelValidate(params).then((data) => {
      if (data.isSuccess) {
        model.validateMessage = null;
        model.validateSuccess = true;
        toast.success(t('Verified Successfully'));
      } else {
        toast.error(t('Verified Failed'));
        model.validateMessage = data.errorMessage;
        model.validateSuccess = false;
      }
      model.validating = false;
      setModels([...modelList.map((x, i) => (i === index ? model : x))]);
    }).catch((error) => {
      console.error('Validation failed:', error);
      toast.error(t('Validation failed'));
      model.validating = false;
      setModels([...modelList.map((x, i) => (i === index ? model : x))]);
    });
  }

  function handleChangeDeploymentName(index: number, value: string) {
    const model = models[index];
    const modelList = [...models];
    model.deploymentName = value;
    setModels(modelList);
  }

  function handleChangeApiType(index: number, value: string) {
    const model = models[index];
    const modelList = [...models];
    model.apiType = Number(value);
    setModels([...modelList.map((x, i) => (i === index ? model : x))]);
  }

  // 过滤模型列表
  const filteredModels = React.useMemo(() => {
    if (!searchQuery.trim()) return models;
    const query = searchQuery.toLowerCase();
    return models.filter(m => m.deploymentName.toLowerCase().includes(query));
  }, [models, searchQuery]);

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="min-w-[375px] w-3/5 max-h-[90vh] overflow-hidden grid-rows-[auto_minmax(0,1fr)]">
        <DialogHeader className="flex-shrink-0">
          <div className="flex items-center gap-3">
            <DialogTitle className="flex-shrink-0">{t('Quick Add Models')}</DialogTitle>
            <Input
              placeholder={t('Search models...')}
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="h-8 max-w-xs"
            />
          </div>
        </DialogHeader>
  <div className="min-h-0 max-h-[65vh] overflow-y-auto pr-2 custom-scrollbar">
          {error ? (
            <div className="flex items-center justify-center w-full h-full text-muted-foreground">
              <div className="text-center max-w-2xl">
                <p className="text-lg">{error}</p>
                <p className="text-sm mt-2">{t('Please use the regular add model method')}</p>
                {errorDetail && (
                  <div className="mt-4 p-4 bg-muted rounded-md text-left">
                    <p className="text-xs font-mono whitespace-pre-wrap break-all">{errorDetail}</p>
                  </div>
                )}
              </div>
            </div>
          ) : loading ? (
            <Table>
              <TableHeader>
                <TableRow className="pointer-events-none">
                  <TableHead>{t('Deployment Name')}</TableHead>
                  <TableHead>{t('API Type')}</TableHead>
                  <TableHead className="w-20">{t('Actions')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {[...Array(5)].map((_, i) => (
                  <TableRow key={i}>
                    <TableCell>
                      <Skeleton className="h-8 w-full" />
                    </TableCell>
                    <TableCell>
                      <Skeleton className="h-8 w-[180px]" />
                    </TableCell>
                    <TableCell>
                      <div className="flex gap-x-2">
                        <Skeleton className="h-6 w-12" />
                        <Skeleton className="h-6 w-12" />
                        <Skeleton className="h-6 w-12" />
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          ) : (
            <Table>
              <TableHeader>
                <TableRow className="pointer-events-none h-9">
                  <TableHead className="py-2">{t('Deployment Name')}</TableHead>
                  <TableHead className="py-2">{t('API Type')}</TableHead>
                  <TableHead className="w-20 py-2">{t('Actions')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filteredModels.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={3} className="text-center text-muted-foreground py-8">
                      {searchQuery ? t('No models found matching your search') : t('No models available')}
                    </TableCell>
                  </TableRow>
                ) : (
                  filteredModels.map((model, index) => {
                    // 找到原始索引以便正确调用处理函数
                    const originalIndex = models.findIndex(m => m.deploymentName === model.deploymentName);
                    return (
                <TableRow key={`${model.deploymentName}-${index}`} className="h-11">
                  <TableCell className="py-1.5">
                    <div className="flex items-center gap-2">
                      {model.existingModel && (
                        <Badge variant="default" className="flex-shrink-0 text-xs py-0">
                          {t('Existed')}
                        </Badge>
                      )}
                      <span className="font-mono text-sm">{model.deploymentName}</span>
                    </div>
                  </TableCell>
                  <TableCell className="py-1.5">
                    <Select
                      value={model.apiType.toString()}
                      onValueChange={(value) => handleChangeApiType(originalIndex, value)}
                      disabled={!!model.existingModel}
                    >
                      <SelectTrigger className="w-[180px] h-8">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="0">ChatCompletion</SelectItem>
                        <SelectItem value="1">Response</SelectItem>
                        <SelectItem value="2">ImageGeneration</SelectItem>
                      </SelectContent>
                    </Select>
                  </TableCell>
                  <TableCell className="py-1.5">
                    <div className="flex gap-x-2 items-center">
                    <Button
                      variant="link"
                      disabled={model.validating}
                      className={`p-0 h-auto text-sm ${model.validateSuccess ? 'text-green-600' : ''}`}
                      onClick={() => {
                        handleValidate(originalIndex);
                      }}
                    >
                      {t('Test')}
                    </Button>
                    
                    {!model.existingModel && (
                      <Button
                        variant="link"
                        disabled={model.adding}
                        className="p-0 h-auto text-sm"
                        onClick={() => {
                          handleAdd(originalIndex);
                        }}
                      >
                        {t('Add')}
                      </Button>
                    )}
                    
                    {onOpenEditModel && (
                      <Button
                        variant="link"
                        className="p-0 h-auto text-sm"
                        onClick={() => {
                          onOpenEditModel(model.deploymentName, model.apiType);
                        }}
                      >
                        {t('Edit')}
                      </Button>
                    )}
                    
                    {model.validateMessage && (
                      <Tips
                        className="h-6"
                        side="bottom"
                        trigger={
                          <Button variant="ghost" className="p-0.5 m-0 h-6 w-6">
                            <IconInfo stroke="#FFD738" size={16} />
                          </Button>
                        }
                        content={
                          <div className="text-wrap w-80">
                            {model.validateMessage}
                          </div>
                        }
                      ></Tips>
                    )}
                    </div>
                  </TableCell>
                </TableRow>
                    );
                  })
                )}
              </TableBody>
            </Table>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
};
export default QuickAddModelModal;
