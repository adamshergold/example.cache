#!/bin/sh -x

export sourceFolder=$1
export targetFolder=$2 

mkdir $targetFolder 

for filename in $sourceFolder/*.nupkg; do

    export tempFolder=`mktemp -d`

    cp $filename $tempFolder

    cd $tempFolder

    export pkg=`basename $filename`

    unzip $pkg

    find . | xargs chmod +r 

    sed -i'.nubak' 's/WILDCARD/*/g' *.nuspec

    rm -f *.nubak 

    rm -f $pkg

    zip -r $pkg *
    
    cp $pkg $targetFolder

    rm -rf $tempFolder 

done