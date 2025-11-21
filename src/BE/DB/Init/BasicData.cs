namespace Chats.BE.DB.Init;

internal static class BasicData
{
    public static void InsertAll(ChatsDB db)
    {
        InsertFinishReasons(db);
        InsertTransactionTypes(db);
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