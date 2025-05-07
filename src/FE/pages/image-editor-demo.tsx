"use client";

import React, { useState } from "react";
import ImageEditor from "@/components/ImageEditor";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export default function ImageEditorDemo() {
  const [imageUrl, setImageUrl] = useState<string>("https://images.unsplash.com/photo-1531804055935-76f44d7c3621");
  const [inputUrl, setInputUrl] = useState<string>("");

  const handleUrlChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setInputUrl(e.target.value);
  };

  const handleLoadImage = () => {
    if (inputUrl.trim()) {
      setImageUrl(inputUrl.trim());
    }
  };

  return (
    <div className="container mx-auto py-8 px-4">
      <h1 className="text-3xl font-bold mb-6">图片编辑器演示</h1>
      
      <Card className="mb-6">
        <CardHeader>
          <CardTitle>图片URL</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex gap-2">
            <Input 
              placeholder="输入图片URL" 
              value={inputUrl} 
              onChange={handleUrlChange}
            />
            <Button onClick={handleLoadImage}>加载</Button>
          </div>
        </CardContent>
      </Card>
      
      <Card>
        <CardHeader>
          <CardTitle>编辑器</CardTitle>
        </CardHeader>
        <CardContent>
          <ImageEditor imageUrl={imageUrl} />
        </CardContent>
      </Card>
      
      <div className="mt-6 text-sm text-gray-500">
        <h3 className="font-medium">使用说明：</h3>
        <ul className="list-disc pl-5 mt-2">
          <li>直接在图片上涂鸦</li>
          <li>使用撤销/重做按钮来撤销或重做上一个操作</li>
          <li>调整画笔大小</li>
          <li>使用缩放按钮或鼠标滚轮来缩放图片</li>
        </ul>
      </div>
    </div>
  );
} 