// Copyright 2015 Oklahoma State University
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

// Guids.cs
// MUST match guids.h
using System;

namespace mageload.extension
{
    static class GuidList
    {
        public const string guidextensionPkgString = "bb99eb3c-b0b5-450b-a131-9c08565f7ed8";
        public const string guidextensionCmdSetString = "b01a2907-cab4-4441-8ada-95a9729996c1";

        public static readonly Guid guidextensionCmdSet = new Guid(guidextensionCmdSetString);
    };
}