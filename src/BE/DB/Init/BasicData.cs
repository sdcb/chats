using Chats.BE.DB;

namespace Chats.BE.DB.Init;

internal static class BasicData
{
    public static void InsertAll(ChatsDB db)
    {
        InsertFileServiceTypes(db);
        InsertChatRoles(db);
        InsertFinishReasons(db);
        InsertStepContentTypes(db);
        InsertKnownImageSizes(db);
        InsertTransactionTypes(db);
    }

    private static void InsertFileServiceTypes(ChatsDB db)
    {
        // Generated from data, hash: 639406aab3eda8539e3adcc6be70809c19a4e7e557a309c7d4fcaadb0281a486
        db.FileServiceTypes.AddRange(
        [
            new(){ Id=0, Name="Local",              InitialConfig="./AppData/Files",                                                                                                                                                                            },
            new(){ Id=1, Name="Minio",              InitialConfig="""{"endpoint": "https://minio.example.com", "accessKey": "your-access-key", "secretKey": "your-secret-key", "bucket": "your-bucket", "region": null}""",                                     },
            new(){ Id=2, Name="AWS S3",             InitialConfig="""{"region": "ap-southeast-1", "accessKeyId": "your-access-key-id", "secretAccessKey": "your-secret-access-key", "bucket": "your-bucket"}""",                                                },
            new(){ Id=3, Name="Aliyun OSS",         InitialConfig="""{"endpoint": "oss-cn-hangzhou.aliyuncs.com", "accessKeyId": "your-access-key-id", "accessKeySecret": "your-access-key-secret", "bucket": "your-bucket"}""",                                },
            new(){ Id=4, Name="Azure Blob Storage", InitialConfig="""{"connectionString": "DefaultEndpointsProtocol=https;AccountName=your-account-name;AccountKey=your-account-key;EndpointSuffix=core.windows.net", "containerName": "YourContainerName"}""", }
        ]);
    }

    private static void InsertChatRoles(ChatsDB db)
    {
        // Generated from data, hash: 4450a3cbc75fbdf25b4af7ed5c400a0f1bc10b873b19b5db6c19f1fbdd7c635e
        db.ChatRoles.AddRange(
        [
            new(){ Id=2, Name="user",      },
            new(){ Id=3, Name="assistant", },
            new(){ Id=4, Name="tool",      }
        ]);
    }

    private static void InsertFinishReasons(ChatsDB db)
    {
        // Generated from data, hash: e63360ff2f0d99db7d3022f771ee424f5b817678f0ec31f82ae607d5c93d2992
        db.FinishReasons.AddRange(
        [
            new(){ Id=0,   Name="Success",             },
            new(){ Id=1,   Name="Stop",                },
            new(){ Id=2,   Name="Length",              },
            new(){ Id=3,   Name="ToolCalls",           },
            new(){ Id=4,   Name="ContentFilter",       },
            new(){ Id=5,   Name="FunctionCall",        },
            new(){ Id=100, Name="UnknownError",        },
            new(){ Id=101, Name="InsufficientBalance", },
            new(){ Id=102, Name="UpstreamError",       },
            new(){ Id=103, Name="InvalidModel",        },
            new(){ Id=104, Name="SubscriptionExpired", },
            new(){ Id=105, Name="BadParameter",        },
            new(){ Id=106, Name="Cancelled",           },
            new(){ Id=107, Name="InternalConfigIssue", }
        ]);
    }

    private static void InsertStepContentTypes(ChatsDB db)
    {
        // Generated from data, hash: 766981cc3e16b456244e4580942ca36c81696dcc2e5463594cb29cf96fc83a4a
        db.StepContentTypes.AddRange(
        [
            new(){ Id=0, ContentType="error",            },
            new(){ Id=1, ContentType="text",             },
            new(){ Id=2, ContentType="fileId",           },
            new(){ Id=3, ContentType="reasoning",        },
            new(){ Id=4, ContentType="toolCall",         },
            new(){ Id=5, ContentType="toolCallResponse", }
        ]);
    }

    private static void InsertKnownImageSizes(ChatsDB db)
    {
        // Generated from data, hash: f6bb405dfbf5ddc5f9a745339a485ee743ffbd2ad0f1ec607cbb94fae93a0e6e
        db.KnownImageSizes.AddRange(
        [
            new(){ Id=0, Width=0,    Height=0,    },
            new(){ Id=1, Width=1024, Height=1024, },
            new(){ Id=2, Width=1536, Height=1024, },
            new(){ Id=3, Width=1024, Height=1536, }
        ]);
    }

    private static void InsertTransactionTypes(ChatsDB db)
    {
        // Generated from data, hash: 03debb1a3f79c1b2fd21af5a0c0ede05a461481794cbe7e0a6a65bd47090e970
        db.TransactionTypes.AddRange(
        [
            new(){ Id=1, Name="Charge",  },
            new(){ Id=2, Name="Cost",    },
            new(){ Id=3, Name="Initial", },
            new(){ Id=4, Name="ApiCost", }
        ]);
    }
};