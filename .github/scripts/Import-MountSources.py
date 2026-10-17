"""Create a small, attributed acquisition snapshot from the public FFXIV Collect API.
No images, flavor text, ownership statistics, prices from markets or character data.
See docs/MOUNT_COLLECTION_METADATA.md for permission and provenance.
"""
import argparse
import datetime
import hashlib
import json
import urllib.request

URL = "https://ffxivcollect.com/api/mounts?language=en"
def snapshot():
    request = urllib.request.Request(URL, headers={"User-Agent": "SentinelHub-MountMetadata/1.0 (https://github.com/MarshalTitan/SentinelHub)", "Accept": "application/json"})
    with urllib.request.urlopen(request, timeout=90) as response:
        raw = response.read(4_000_001)
    if len(raw) > 4_000_000:
        raise ValueError("API response exceeds bound")
    body = json.loads(raw)
    rows = body["results"]
    if not isinstance(rows, list) or not 100 <= len(rows) <= 2000 or body.get("count") != len(rows):
        raise ValueError("Incomplete or unexpected result set")
    def text(value, limit):
        if not isinstance(value, str) or len(value) > limit or any(ord(c) < 32 for c in value):
            raise ValueError("Unexpected source string")
        return value
    result = []
    ids = set()
    for row in rows:
        mid = row["id"]
        if type(mid) is not int or mid <= 0 or mid in ids:
            raise ValueError("Invalid or duplicate mount ID")
        ids.add(mid)
        sources = []
        for source in row["sources"]:
            kind = text(source["type"], 80)
            description = text(source["text"], 1000)
            if kind == "Unknown" or not description.strip():
                continue
            sources.append({"Type":kind, "Text":description})
        result.append({"Id":mid, "EnglishName":text(row["name"], 200), "Patch":text(row["patch"], 20), "Sources":sources})
    return {"Provider":"FFXIV Collect", "Endpoint":URL,
        "RetrievedUtc":datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "ResponseSha256":hashlib.sha256(raw).hexdigest(),
        "Permission":"https://ffxivcollect.com/ ; https://www.reddit.com/r/ffxiv/comments/i75cb0/ffxiv_collect_whats_new/",
        "Mounts":sorted(result, key=lambda row:row["Id"])}

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--output")
    parser.add_argument("--audit-log", action="store_true")
    args = parser.parse_args()
    data = snapshot()
    encoded = json.dumps(data, ensure_ascii=True, separators=(",", ":"))
    if args.output:
        with open(args.output, "w", encoding="utf-8", newline="\n") as output:
            output.write(json.dumps(data, ensure_ascii=True, indent=2) + "\n")
    if args.audit_log:
        print("MOUNT_SOURCE_SNAPSHOT=" + encoded)
    kinds = sorted({source["Type"] for mount in data["Mounts"] for source in mount["Sources"]})
    print(json.dumps({"mounts":len(data["Mounts"]), "withSources":sum(bool(m["Sources"]) for m in data["Mounts"]), "types":kinds, "responseSha256":data["ResponseSha256"]}))
